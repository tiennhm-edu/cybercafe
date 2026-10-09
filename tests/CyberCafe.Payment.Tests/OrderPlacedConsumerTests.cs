// ============================================================================
// OrderPlacedConsumerTests.cs — consumer idempotent của Payment service (Buổi 55).
// MassTransit test harness: bus TRONG BỘ NHỚ thật (publish → consumer → publish kết quả), không cần RabbitMQ.
// harness.Published / Consumed ghi lại mọi message → kiểm tra "đã gửi gì, MessageId nào".
// ⚠️ Harness gộp các message CÙNG MessageId thành 1 bản ghi → muốn ĐẾM số lần nhận, dùng consumer đếm riêng (ResultLog).
// 👉 Bước 5 (b55.md)
// ============================================================================
using System.Collections.Concurrent;
using CyberCafe.IntegrationEvents;
using CyberCafe.Payment.Api.Data;
using CyberCafe.Payment.Api.Messaging;
using CyberCafe.Payment.Api.Processing;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CyberCafe.Payment.Tests;

public sealed class OrderPlacedConsumerTests : IAsyncLifetime
{
    private readonly string _databaseName = $"PaymentTests-{Guid.NewGuid()}";
    private ServiceProvider _provider = default!;
    private ITestHarness _harness = default!;
    private readonly ResultLog _results = new();

    public async Task InitializeAsync()
    {
        // DI giống Program.cs của Payment service, chỉ đổi SQL Server → InMemory và RabbitMQ → test harness
        this._provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<PaymentDbContext>(o => o.UseInMemoryDatabase(this._databaseName))
            .AddSingleton(Options.Create(new PaymentRulesOptions { MaxAmount = 500_000m, DeclinedCardSuffix = "0000", SimulatedDelayMs = 0 }))
            .AddSingleton<PaymentRules>()
            .AddSingleton(TimeProvider.System)
            .AddSingleton(this._results)
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<OrderPlacedConsumer>();
                bus.AddConsumer<ResultCounter>(); // đóng vai Order service: ghi lại mọi PaymentCompleted nhận được
            })
            .BuildServiceProvider(true);
        this._harness = this._provider.GetRequiredService<ITestHarness>();
        await this._harness.Start();
    }

    public async Task DisposeAsync()
    {
        await this._harness.Stop();
        await this._provider.DisposeAsync();
    }

    private static OrderPlacedIntegrationEvent Placed(int orderId, decimal amount, string method = "Card", string? last4 = "1234") =>
        new(Guid.NewGuid(), DateTime.UtcNow, orderId, $"CC-{orderId:0000}", amount, method, last4);

    // Gửi message rồi chờ consumer xử lý xong lần thứ <paramref name="expectedTotal"/>.
    // MessageId của broker để MassTransit tự sinh (mỗi lần 1 Id) — consumer chống trùng theo EventId TRONG message.
    private async Task PublishAndWaitAsync(OrderPlacedIntegrationEvent message, int expectedTotal)
    {
        await this._harness.Bus.Publish(message);
        IConsumerTestHarness<OrderPlacedConsumer> consumer = this._harness.GetConsumerHarness<OrderPlacedConsumer>();
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (consumer.Consumed.Select<OrderPlacedIntegrationEvent>().Count() < expectedTotal && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(await consumer.Consumed.Any<OrderPlacedIntegrationEvent>());
    }

    private async Task<List<PaymentTransaction>> TransactionsAsync()
    {
        using IServiceScope scope = this._provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Transactions.ToListAsync();
    }

    // Kiểm tra: đơn hợp lệ → lưu giao dịch Succeeded + publish PaymentCompleted mang MessageId = ResultEventId đã lưu.
    [Fact]
    public async Task ValidOrder_SavesSucceededTransaction_AndPublishesCompleted()
    {
        await this.PublishAndWaitAsync(Placed(7, 68000m), 1);

        Assert.True(await this._harness.Published.Any<PaymentCompletedIntegrationEvent>());
        PaymentTransaction tx = Assert.Single(await this.TransactionsAsync());
        IPublishedMessage<PaymentCompletedIntegrationEvent> published = this._harness.Published.Select<PaymentCompletedIntegrationEvent>().Single();
        Assert.Equal((7, PaymentStatus.Succeeded, "1234"), (tx.OrderId, tx.Status, tx.CardLast4));
        Assert.Equal(tx.ResultEventId, published.Context.Message.EventId);
        Assert.Equal(tx.ResultEventId, published.Context.MessageId);
        Assert.Equal(tx.TransactionId, published.Context.Message.TransactionId);
    }

    // Kiểm tra: vượt hạn mức → giao dịch Failed + PaymentFailed có lý do; KHÔNG có PaymentCompleted.
    [Fact]
    public async Task OverLimit_PublishesFailed()
    {
        await this.PublishAndWaitAsync(Placed(8, 900_000m, "Cash", null), 1);

        Assert.True(await this._harness.Published.Any<PaymentFailedIntegrationEvent>());
        PaymentFailedIntegrationEvent failed = this._harness.Published.Select<PaymentFailedIntegrationEvent>().Single().Context.Message;
        Assert.Equal(8, failed.OrderId);
        Assert.Contains("Vượt hạn mức", failed.Reason);
        Assert.False(this._harness.Published.Select<PaymentCompletedIntegrationEvent>().Any());
        Assert.Equal(PaymentStatus.Failed, Assert.Single(await this.TransactionsAsync()).Status);
    }

    // Kiểm tra: CÙNG message tới 2 lần (at-least-once) → chỉ 1 giao dịch; lần 2 gửi LẠI kết quả với ĐÚNG EventId cũ
    // (để Order service — cũng idempotent — nhận ra là trùng).
    [Fact]
    public async Task DuplicateMessage_ChargesOnce_AndReplaysSameResult()
    {
        OrderPlacedIntegrationEvent message = Placed(9, 68000m);

        await this.PublishAndWaitAsync(message, 1);
        await this.PublishAndWaitAsync(message, 2);

        PaymentTransaction tx = Assert.Single(await this.TransactionsAsync());
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (this._results.EventIds.Count < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal([tx.ResultEventId, tx.ResultEventId], this._results.EventIds.ToArray());
    }

    // Kiểm tra: khóa NGHIỆP VỤ — message KHÁC EventId nhưng cùng OrderId (Order service gửi lại kiểu mới) vẫn không thu 2 lần.
    [Fact]
    public async Task SameOrderDifferentEventId_StillOneTransaction()
    {
        await this.PublishAndWaitAsync(Placed(10, 68000m), 1);
        await this.PublishAndWaitAsync(Placed(10, 68000m), 2);

        Assert.Single(await this.TransactionsAsync());
    }
}

/// <summary>Nơi ghi EventId của các PaymentCompleted đã nhận (dùng chung giữa test và ResultCounter).</summary>
public sealed class ResultLog
{
    /// <summary>EventId theo thứ tự nhận.</summary>
    public ConcurrentQueue<Guid> EventIds { get; } = new();
}

/// <summary>Consumer giả "phía Order service": chỉ ghi lại kết quả nhận được.</summary>
public sealed class ResultCounter(ResultLog log) : IConsumer<PaymentCompletedIntegrationEvent>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> context)
    {
        log.EventIds.Enqueue(context.Message.EventId);
        return Task.CompletedTask;
    }
}
