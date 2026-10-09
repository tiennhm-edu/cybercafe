// ============================================================================
// MessagingFlowTests.cs — luồng thanh toán bất đồng bộ ở tầng Application (Buổi 55 · Outbox + idempotent consumer).
// Không RabbitMQ, không database: chạy pipeline THẬT (AddApplication) với port giả có "transaction"
// (TransactionalUnitOfWork) → kiểm tra đúng điều quan trọng nhất của Outbox: đơn và message nằm trong CÙNG transaction.
// 👉 Bước 2 · Bước 4 · Bước 5 (b55.md)
// ============================================================================
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Orders;
using CyberCafe.Application.Orders.Commands;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Application.Products;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using CyberCafe.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Tests;

public class MessagingFlowTests
{
    private readonly FakeOrderRepository _orders = new();
    private readonly LogSink _logs = new();

    // DI như Api khi Payments:Flow = Messaging: AddApplication() + OrderingSettings(Messaging) + port giả
    private ServiceProvider BuildServices(IUnitOfWork unitOfWork, IIntegrationEventOutbox outbox, FakeInbox? inbox = null)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(new OrderingSettings(PaymentFlow.Messaging));
        services.AddSingleton(this._logs);
        services.AddSingleton(typeof(ILogger<>), typeof(ListLogger<>));
        services.AddSingleton<IProductRepository>(new FakeProductRepository(Sample.CaPheSua(), Sample.Tiramisu()));
        services.AddSingleton<IOrderRepository>(this._orders);
        services.AddSingleton(unitOfWork);
        services.AddSingleton(outbox);
        services.AddSingleton<IInbox>(inbox ?? new FakeInbox());
        services.AddSingleton<IOrderReadStore>(new FakeOrderReadStore());
        services.AddSingleton<IOrderNotifier>(new RecordingNotifier());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static PlaceOrderCommand CardOrder() => new(3, "Nguyễn Văn An", "0901234567",
        [new OrderLineRequest(1, DrinkSize.M, 2)], null, PaymentMethod.Card, "4111111111111234", null);

    // Đơn chờ thanh toán, Id giả như đã lưu DB
    private static Order AwaitingOrder(int id)
    {
        Order order = Order.Create(new Customer("An", "0901234567"), null, ownerId: 3);
        order.AddItem(Sample.CaPheSua(), DrinkSize.M, 2); // 2 × 34.000 = 68.000
        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, id);
        return order;
    }

    // Kiểm tra: Messaging → đơn KHÔNG được Pay ngay; lưu 2 lần (lấy Id rồi ghi outbox), CẢ 2 lần + Enqueue đều nằm
    // trong 1 transaction đã commit; event mang đúng OrderId/số tiền và chỉ 4 số cuối thẻ.
    [Fact]
    public async Task PlaceOrder_Messaging_WritesOrderAndOutboxInSameTransaction()
    {
        TransactionalUnitOfWork uow = new(this._orders);
        RecordingOutbox outbox = new(uow);
        using ServiceProvider sp = this.BuildServices(uow, outbox);
        using IServiceScope scope = sp.CreateScope();

        OrderDto dto = await scope.ServiceProvider.GetRequiredService<ISender>().Send(CardOrder());

        Order order = Assert.Single(this._orders.Added);
        Assert.True(order.IsAwaitingPayment);
        Assert.Null(dto.PaymentMethod);
        Assert.Empty(order.DomainEvents.OfType<OrderPlaced>()); // chưa trả tiền → chưa lên quầy
        Assert.Equal([true, true], uow.Saves);                  // 2 lần lưu, đều TRONG transaction
        Assert.Equal([true], uow.Outcomes);                     // 1 transaction, commit
        (IIntegrationEvent evt, bool inTransaction) = Assert.Single(outbox.Enqueued);
        Assert.True(inTransaction);
        OrderPlacedIntegrationEvent placed = Assert.IsType<OrderPlacedIntegrationEvent>(evt);
        Assert.Equal((order.Id, 68000m, "Card", "1234"), (placed.OrderId, placed.Amount, placed.PaymentMethod, placed.CardLast4));
        Assert.Equal(order.Code.Value, placed.OrderCode);
    }

    // Kiểm tra: ghi outbox (lần lưu 2) lỗi → exception đi ra ngoài, transaction ROLLBACK → đơn ở lần lưu 1 cũng mất theo
    // (không bao giờ có đơn mà Payment không được báo).
    [Fact]
    public async Task PlaceOrder_Messaging_OutboxSaveFails_RollsBackWholeTransaction()
    {
        TransactionalUnitOfWork uow = new(this._orders) { FailOnSave = 2 };
        using ServiceProvider sp = this.BuildServices(uow, new RecordingOutbox(uow));
        using IServiceScope scope = sp.CreateScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ISender>().Send(CardOrder()));

        Assert.Equal([false], uow.Outcomes);
    }

    // Kiểm tra: PaymentCompleted → Pay (OrderPaid + OrderPlaced), đánh dấu inbox; gửi lại CÙNG EventId → Duplicate,
    // không cộng điểm lần 2, không lưu thêm.
    [Fact]
    public async Task ConfirmPayment_AppliesOnce_DuplicateIsIgnored()
    {
        Order order = AwaitingOrder(7);
        this._orders.Stored[7] = order;
        TransactionalUnitOfWork uow = new(this._orders);
        FakeInbox inbox = new();
        using ServiceProvider sp = this.BuildServices(uow, new RecordingOutbox(uow), inbox);
        ConfirmOrderPaymentCommand command = new(Guid.NewGuid(), 7, 68000m, "Card", "1234", "TX-1");

        PaymentResultOutcome first, second;
        using (IServiceScope scope = sp.CreateScope())
        {
            first = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        int pointsAfterFirst = order.Customer.LoyaltyPoints;
        using (IServiceScope scope = sp.CreateScope())
        {
            second = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        Assert.Equal((PaymentResultOutcome.Applied, PaymentResultOutcome.Duplicate), (first, second));
        Assert.True(order.IsPaid);
        Assert.Equal("****1234", Assert.IsType<CardPayment>(order.Payment).CardNumber);
        Assert.Single(order.DomainEvents.OfType<OrderPlaced>());
        Assert.Equal(6, pointsAfterFirst);
        Assert.Equal(pointsAfterFirst, order.Customer.LoyaltyPoints);
        Assert.Single(uow.Saves);
        Assert.Contains((command.EventId, nameof(ConfirmOrderPaymentCommand)), inbox.Processed);
    }

    // Kiểm tra: PaymentFailed → đơn bị hủy (OrderStatusChanged → SignalR báo khách); đơn đã trả tiền thì KHÔNG bị hủy
    // bởi 1 message thất bại đến trễ (Ignored) nhưng vẫn đánh dấu đã xử lý.
    [Fact]
    public async Task RejectPayment_CancelsAwaitingOrder_ButNeverAPaidOne()
    {
        Order awaiting = AwaitingOrder(8);
        Order paid = Sample.PaidOrder(9, ownerId: 3);
        this._orders.Stored[8] = awaiting;
        this._orders.Stored[9] = paid;
        TransactionalUnitOfWork uow = new(this._orders);
        FakeInbox inbox = new();
        using ServiceProvider sp = this.BuildServices(uow, new RecordingOutbox(uow), inbox);
        using IServiceScope scope = sp.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        PaymentResultOutcome cancelled = await sender.Send(new RejectOrderPaymentCommand(Guid.NewGuid(), 8, "Vượt hạn mức"));
        RejectOrderPaymentCommand late = new(Guid.NewGuid(), 9, "Thẻ bị từ chối");
        PaymentResultOutcome ignored = await sender.Send(late);

        Assert.Equal((PaymentResultOutcome.Applied, PaymentResultOutcome.Ignored), (cancelled, ignored));
        Assert.Equal(OrderStatus.Cancelled, awaiting.Status);
        Assert.Single(awaiting.DomainEvents.OfType<OrderStatusChanged>());
        Assert.Equal(OrderStatus.Pending, paid.Status);
        Assert.Contains((late.EventId, nameof(RejectOrderPaymentCommand)), inbox.Processed);
    }

    // Kiểm tra: khách đã tự hủy trong lúc chờ → PaymentCompleted đến sau bị bỏ qua (log "cần hoàn tiền"), không ném lỗi
    // (ném lỗi = MassTransit retry mãi 1 message không bao giờ thành công).
    [Fact]
    public async Task ConfirmPayment_ForCancelledOrder_IsIgnoredAndLogged()
    {
        Order order = AwaitingOrder(10);
        order.Cancel(requestedByCustomer: true);
        this._orders.Stored[10] = order;
        TransactionalUnitOfWork uow = new(this._orders);
        using ServiceProvider sp = this.BuildServices(uow, new RecordingOutbox(uow));
        using IServiceScope scope = sp.CreateScope();

        PaymentResultOutcome outcome = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ConfirmOrderPaymentCommand(Guid.NewGuid(), 10, 68000m, "Cash", null, "TX-2"));

        Assert.Equal(PaymentResultOutcome.Ignored, outcome);
        Assert.False(order.IsPaid);
        Assert.Contains(this._logs.Lines, l => l.StartsWith("Warning") && l.Contains("cần hoàn tiền"));
    }
}
