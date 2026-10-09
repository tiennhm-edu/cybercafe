// ============================================================================
// PipelineTests.cs — dispatcher + pipeline behavior + domain event publisher (Buổi 51–52 · 55).
// Dựng DI THẬT bằng AddApplication() (giống Program.cs), chỉ thay port bằng bản giả → kiểm tra đúng
// cái mà Api sẽ chạy: thứ tự Logging → Validation → Transaction → Handler.
// 👉 Bước 5 (b52.md)
// ============================================================================
using CyberCafe.Application.Caching;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Orders;
using CyberCafe.Application.Orders.Commands;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Application.Products;
using CyberCafe.Application.Products.Queries;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Tests;

public class PipelineTests
{
    private readonly FakeProductRepository _products = new(Sample.CaPheSua(), Sample.Tiramisu());
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly LogSink _logs = new();

    // DI giống Api: AddApplication() thật + port giả + logger ghi vào danh sách
    private ServiceProvider BuildServices(Action<IServiceCollection>? extra = null)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(this._logs);
        services.AddSingleton(typeof(ILogger<>), typeof(ListLogger<>));
        services.AddSingleton<IProductRepository>(this._products);
        services.AddSingleton<IOrderRepository>(new FakeOrderRepository());
        services.AddSingleton<IUnitOfWork>(this._unitOfWork);
        services.AddSingleton<IOrderReadStore>(new FakeOrderReadStore());
        services.AddSingleton<IOrderNotifier>(new RecordingNotifier());
        services.AddSingleton<IMenuCache>(new PassThroughCache());
        // Buổi 55: handler kết quả thanh toán (ConfirmOrderPayment...) cần IInbox; PlaceOrder nhận thêm outbox (tùy chọn)
        services.AddSingleton<IInbox>(new FakeInbox());
        services.AddSingleton<IIntegrationEventOutbox>(new RecordingOutbox());
        extra?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static PlaceOrderCommand ValidCommand() => new(3, "Nguyễn Văn An", "0901234567",
        [new OrderLineRequest(1, DrinkSize.M, 2)], null, PaymentMethod.Cash, null, null);

    // Kiểm tra: command hợp lệ đi qua đủ pipeline — có transaction, có log bắt đầu/kết thúc, handler chạy (lưu 1 lần).
    [Fact]
    public async Task Command_RunsInsideTransaction_AndIsLogged()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();

        OrderDto dto = await scope.ServiceProvider.GetRequiredService<ISender>().Send(ValidCommand());

        Assert.Equal(68000, dto.FinalAmount);
        Assert.Equal((1, 1), (this._unitOfWork.Transactions, this._unitOfWork.Saves));
        Assert.Contains(this._logs.Lines, l => l.Contains("→ PlaceOrderCommand"));
        Assert.Contains(this._logs.Lines, l => l.Contains("✓ PlaceOrderCommand"));
    }

    // Kiểm tra: query KHÔNG mở transaction (TransactionBehavior chỉ bọc ICommand).
    [Fact]
    public async Task Query_SkipsTransaction()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();

        CacheResult<PagedResult<ProductDto>> menu = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMenuQuery(new ProductQuery()));

        Assert.Equal(2, menu.Value.TotalCount);
        Assert.Equal(0, this._unitOfWork.Transactions);
    }

    // Kiểm tra: dữ liệu sai → ValidationBehavior chặn TRƯỚC handler (không tra món, không mở transaction),
    // lỗi gom theo field kiểu "Items[0].Quantity"; LoggingBehavior (ngoài cùng) vẫn ghi nhận thất bại.
    [Fact]
    public async Task InvalidCommand_StopsAtValidation_BeforeTransactionAndHandler()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();
        PlaceOrderCommand bad = ValidCommand() with { PhoneNumber = "123", Items = [new OrderLineRequest(1, DrinkSize.S, 50)], PaymentMethod = PaymentMethod.Card };

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() => scope.ServiceProvider.GetRequiredService<ISender>().Send(bad));

        Assert.Contains("PhoneNumber", ex.Errors.Keys);
        Assert.Contains("Items[0].Quantity", ex.Errors.Keys);
        Assert.Contains("CardNumber", ex.Errors.Keys);
        Assert.Equal((0, 0), (this._products.Lookups, this._unitOfWork.Transactions));
        Assert.Contains(this._logs.Lines, l => l.Contains("✗ PlaceOrderCommand") && l.Contains(nameof(ValidationException)));
    }

    // Kiểm tra: query sai tham số (GetMenuQuery pageSize 1000) cũng bị validator chặn.
    [Fact]
    public async Task InvalidQuery_IsRejectedToo()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMenuQuery(new ProductQuery { PageSize = 1000, SortBy = "hack" })));

        Assert.Equal(2, ex.Errors.Count);
    }

    // Kiểm tra: gửi request chưa có handler → lỗi rõ ràng (không phải NullReferenceException khó hiểu).
    [Fact]
    public async Task Send_WithoutHandler_ThrowsHelpfulError()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>().Send(new OrphanQuery()));

        Assert.Contains(nameof(OrphanQuery), ex.Message);
    }

    // Kiểm tra: AddApplication tìm đủ handler cho MỌI command/query trong assembly (quên đăng ký là test đỏ).
    [Fact]
    public void EveryRequest_HasRegisteredHandler()
    {
        using ServiceProvider sp = this.BuildServices();
        using IServiceScope scope = sp.CreateScope();
        Type[] requests = typeof(PlaceOrderCommand).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToArray();

        foreach (Type request in requests)
        {
            Type response = request.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)).GetGenericArguments()[0];
            Assert.NotNull(scope.ServiceProvider.GetService(typeof(IRequestHandler<,>).MakeGenericType(request, response)));
        }

        Assert.True(requests.Length >= 10); // 3 command đơn + 3 query đơn + 2 query thực đơn + (b55) 2 command kết quả thanh toán
    }

    // Kiểm tra: publisher gọi MỌI handler của event; 1 handler lỗi không chặn handler khác và không ném ra ngoài.
    [Fact]
    public async Task DomainEventPublisher_IsolatesFailingHandler()
    {
        RecordingNotifier notifier = new();
        using ServiceProvider sp = this.BuildServices(s =>
        {
            s.AddSingleton<IOrderNotifier>(notifier);
            s.AddScoped<IDomainEventHandler<OrderPlaced>, ExplodingHandler>(); // thêm 1 handler hỏng cho OrderPlaced
        });
        using IServiceScope scope = sp.CreateScope();
        Order order = Sample.PaidOrder(5, ownerId: 3);

        await scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>().PublishAsync([new OrderPlaced(order, DateTime.Now)]);

        Assert.Equal(5, Assert.Single(notifier.Placed).Id);
        Assert.Contains(this._logs.Lines, l => l.StartsWith("Error") && l.Contains(nameof(ExplodingHandler)));
    }

    /// <summary>Query không có handler nào (chỉ dùng trong test).</summary>
    private sealed record OrphanQuery : IQuery<int>;

    /// <summary>Handler luôn lỗi — mô phỏng SignalR/email sập.</summary>
    private sealed class ExplodingHandler : IDomainEventHandler<OrderPlaced>
    {
        public Task Handle(OrderPlaced domainEvent, CancellationToken ct) => throw new InvalidOperationException("Mất kết nối");
    }

    /// <summary>Cache "không cache": luôn gọi factory (MISS).</summary>
    private sealed class PassThroughCache : IMenuCache
    {
        public async Task<CacheResult<T>> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) =>
            new(await factory(ct), Hit: false);

        public Task InvalidateAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
