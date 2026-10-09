// ============================================================================
// PaymentMessagingTests.cs — Order service với Payments:Flow = Messaging, chạy NGUYÊN Api (Buổi 55).
// Không RabbitMQ: MassTransit dùng bus trong bộ nhớ của Api. Test đóng vai Payment service — publish
// PaymentCompleted / PaymentFailed lên bus → consumer thật → command handler → aggregate → domain event → notifier.
// Luồng: POST /api/orders → (outbox) → … → PaymentCompleted → đơn Paid → OrderPlaced → barista.
// 👉 Bước 2 · Bước 4 · Bước 5 (b55.md)
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using CyberCafe.Infrastructure.Messaging;
using CyberCafe.IntegrationEvents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CyberCafe.Api.Tests;

public class PaymentMessagingTests : IAsyncLifetime
{
    private readonly CyberCafeApiFactory _factory = new() { PaymentFlow = "Messaging" };
    private HttpClient _customer = default!;
    private HttpClient _barista = default!;

    public async Task InitializeAsync()
    {
        this._customer = await this._factory.CustomerAsync();
        this._barista = await this._factory.BaristaAsync();
    }

    public async Task DisposeAsync() => await this._factory.DisposeAsync();

    // Chờ tới khi điều kiện trên DB đúng (consumer/worker chạy nền, bất đồng bộ) — tối đa 10 giây
    private async Task WaitForAsync(Func<CyberCafeDbContextProbe, Task<bool>> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            bool done = false;
            await this._factory.WithDbAsync(async db => done = await condition(new CyberCafeDbContextProbe(db)));
            if (done)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("Hết thời gian chờ xử lý bất đồng bộ");
    }

    private Task PublishAsync<T>(T message)
        where T : class, IIntegrationEvent =>
        this._factory.Services.GetRequiredService<IBus>().Publish(message);

    private async Task<OrderDto> GetOrderAsync(int id) =>
        await (await this._customer.GetAsync($"/api/orders/{id}")).ReadAsync<OrderDto>();

    private async Task<int> BoardCountAsync() =>
        (await (await this._barista.GetAsync("/api/orders")).ReadAsync<PagedResult<OrderDto>>()).TotalCount;

    // Kiểm tra: đặt hàng → 201 nhưng CHƯA thanh toán; cùng lúc có 1 dòng outbox OrderPlaced (đúng OrderId, chỉ 4 số cuối thẻ);
    // worker nền gửi dòng đó lên bus (ProcessedAtUtc); quầy KHÔNG thấy đơn, barista chưa được báo.
    [Fact]
    public async Task PlaceOrder_SavesAwaitingOrder_AndOutboxIsDispatched()
    {
        PlaceOrderRequest request = TestData.Order();
        request.PaymentMethod = Domain.Payments.PaymentMethod.Card;
        request.CardNumber = "4111111111111234";

        HttpResponseMessage response = await this._customer.PostAsJsonAsync("/api/orders", request, CyberCafeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrderDto order = await response.ReadAsync<OrderDto>();
        Assert.Equal((OrderStatus.Pending, (string?)null), (order.Status, order.PaymentText));
        await this.WaitForAsync(async db => await db.Outbox.AnyAsync(m => m.ProcessedAtUtc != null));
        await this._factory.WithDbAsync(async db =>
        {
            OutboxMessage row = await db.OutboxMessages.SingleAsync();
            OrderPlacedIntegrationEvent evt = Assert.IsType<OrderPlacedIntegrationEvent>(row.ToIntegrationEvent());
            Assert.Equal((order.Id, order.FinalAmount, "Card", "1234"), (evt.OrderId, evt.Amount, evt.PaymentMethod, evt.CardLast4));
            Assert.DoesNotContain("4111111111111234", row.Payload);
        });
        Assert.Equal(0, await this.BoardCountAsync());
        Assert.Empty(this._factory.Notifier.Placed);
    }

    // Kiểm tra: PaymentCompleted → đơn Paid, lên bảng quầy, barista được báo ĐÚNG 1 lần; gửi lại CÙNG EventId → không đổi gì
    // (1 dòng ProcessedMessages, vẫn 1 thông báo).
    [Fact]
    public async Task PaymentCompleted_PaysOrderOnce_EvenIfDeliveredTwice()
    {
        OrderDto order = await this._customer.PlaceAsync();
        PaymentCompletedIntegrationEvent completed = new(Guid.NewGuid(), DateTime.UtcNow, order.Id, order.FinalAmount, "Cash", null, "PAY-TEST-1");

        await this.PublishAsync(completed);
        await this.WaitForAsync(async db => await db.Processed.CountAsync() == 1);
        await this.PublishAsync(completed);
        await Task.Delay(300); // cho message thứ 2 kịp chạy (nó sẽ bị bỏ qua)

        OrderDto paid = await this.GetOrderAsync(order.Id);
        Assert.NotNull(paid.PaymentText);
        Assert.Equal(1, await this.BoardCountAsync());
        Assert.Equal(order.Id, Assert.Single(this._factory.Notifier.Placed).Id);
        await this._factory.WithDbAsync(async db => Assert.Equal(1, await db.ProcessedMessages.CountAsync()));
    }

    // Kiểm tra: PaymentFailed → đơn Cancelled (khách nhận OrderStatusChanged qua notifier), không bao giờ lên quầy.
    [Fact]
    public async Task PaymentFailed_CancelsOrder()
    {
        OrderDto order = await this._customer.PlaceAsync();

        await this.PublishAsync(new PaymentFailedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, order.Id, "Vượt hạn mức"));
        await this.WaitForAsync(async db => await db.Processed.AnyAsync());

        Assert.Equal(OrderStatus.Cancelled, (await this.GetOrderAsync(order.Id)).Status);
        Assert.Equal(OrderStatus.Cancelled, Assert.Single(this._factory.Notifier.StatusChanged).Status);
        Assert.Equal(0, await this.BoardCountAsync());
    }

    /// <summary>Gom 2 DbSet hay hỏi trong lúc chờ.</summary>
    public sealed class CyberCafeDbContextProbe(Infrastructure.Persistence.CyberCafeDbContext db)
    {
        public IQueryable<OutboxMessage> Outbox => db.OutboxMessages.AsNoTracking();

        public IQueryable<ProcessedMessage> Processed => db.ProcessedMessages.AsNoTracking();
    }
}
