// ============================================================================
// OutboxDispatcherTests.cs — OutboxDispatcher + OutboxMessage (Buổi 55 · Outbox pattern).
// Gọi THẲNG 1 lượt quét (DispatchPendingAsync) với DbContext InMemory + bus giả → không chờ timer, không RabbitMQ.
// 👉 Bước 3 (b55.md)
// ============================================================================
using System.Diagnostics;
using CyberCafe.Infrastructure.Messaging;
using CyberCafe.Infrastructure.Persistence;
using CyberCafe.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CyberCafe.Api.Tests;

public class OutboxDispatcherTests
{
    private readonly DbContextOptions<CyberCafeDbContext> _options = new DbContextOptionsBuilder<CyberCafeDbContext>()
        .UseInMemoryDatabase($"Outbox-{Guid.NewGuid()}")
        .Options;

    private static OrderPlacedIntegrationEvent Placed(int orderId, DateTime occurredAtUtc) =>
        new(Guid.NewGuid(), occurredAtUtc, orderId, $"CC-{orderId:0000}", 68000m, "Card", "1234");

    private async Task SeedAsync(params (IIntegrationEvent Event, string? TraceParent)[] messages)
    {
        await using CyberCafeDbContext db = new(this._options);
        db.OutboxMessages.AddRange(messages.Select(m => OutboxMessage.From(m.Event, m.TraceParent)));
        await db.SaveChangesAsync();
    }

    private async Task<int> DispatchAsync(FakeBus bus)
    {
        await using CyberCafeDbContext db = new(this._options);
        return await new OutboxDispatcher(db, bus, TimeProvider.System, NullLogger<OutboxDispatcher>.Instance).DispatchPendingAsync();
    }

    private async Task<List<OutboxMessage>> RowsAsync()
    {
        await using CyberCafeDbContext db = new(this._options);
        return await db.OutboxMessages.AsNoTracking().ToListAsync();
    }

    // Kiểm tra: dòng outbox giữ đúng kiểu + dữ liệu + traceparent; đọc lại ra ĐÚNG record ban đầu (JSON 2 chiều).
    [Fact]
    public void OutboxMessage_RoundTripsEventAndTraceParent()
    {
        OrderPlacedIntegrationEvent original = Placed(7, DateTime.UtcNow);
        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        OutboxMessage row = OutboxMessage.From(original, traceParent);

        Assert.Equal(original.EventId, row.Id);
        Assert.Equal(typeof(OrderPlacedIntegrationEvent).FullName, row.Type);
        Assert.Equal(traceParent, row.TraceParent);
        Assert.Null(row.ProcessedAtUtc);
        Assert.Equal(original, row.ToIntegrationEvent()); // record so sánh theo GIÁ TRỊ
    }

    // Kiểm tra: 1 lượt quét gửi mọi dòng chưa gửi theo thứ tự OccurredAt, đánh dấu ProcessedAtUtc; lượt sau không gửi lại.
    [Fact]
    public async Task Dispatch_PublishesInOrder_AndMarksSent()
    {
        DateTime now = DateTime.UtcNow;
        OrderPlacedIntegrationEvent later = Placed(2, now), earlier = Placed(1, now.AddSeconds(-5));
        await this.SeedAsync((later, null), (earlier, null));
        FakeBus bus = new();

        int first = await this.DispatchAsync(bus);
        int second = await this.DispatchAsync(bus);

        Assert.Equal((2, 0), (first, second));
        Assert.Equal([earlier.EventId, later.EventId], bus.Published.Select(e => e.EventId).ToArray());
        Assert.All(await this.RowsAsync(), r => Assert.NotNull(r.ProcessedAtUtc));
    }

    // Kiểm tra: broker lỗi → KHÔNG mất message: dòng giữ nguyên chưa gửi, Attempts++, LastError; broker sống lại → lượt sau gửi được.
    [Fact]
    public async Task Dispatch_BrokerDown_KeepsMessageForRetry()
    {
        await this.SeedAsync((Placed(3, DateTime.UtcNow), null));

        int sentWhileDown = await this.DispatchAsync(new FakeBus { FailWith = "RabbitMQ không kết nối được" });
        OutboxMessage afterFailure = Assert.Single(await this.RowsAsync());
        int sentAfterRecovery = await this.DispatchAsync(new FakeBus());

        Assert.Equal((0, 1), (sentWhileDown, sentAfterRecovery));
        Assert.Equal((1, "RabbitMQ không kết nối được"), (afterFailure.Attempts, afterFailure.LastError));
        Assert.Null(afterFailure.ProcessedAtUtc);
        Assert.NotNull(Assert.Single(await this.RowsAsync()).ProcessedAtUtc);
    }

    // Kiểm tra: span "outbox publish" là CON của trace đã lưu trong dòng outbox → dashboard nối request POST với lần publish.
    [Fact]
    public async Task Dispatch_ContinuesTraceFromStoredTraceParent()
    {
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        await this.SeedAsync((Placed(4, DateTime.UtcNow), $"00-{traceId}-b7ad6b7169203331-01"));
        List<Activity> spans = [];
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == OutboxDispatcher.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);

        await this.DispatchAsync(new FakeBus());

        Activity span = Assert.Single(spans, s => s.TraceId.ToHexString() == traceId);
        Assert.Equal(ActivityKind.Producer, span.Kind);
        Assert.Contains("OrderPlacedIntegrationEvent", span.DisplayName);
    }

    /// <summary>Bus giả: ghi lại event đã publish, hoặc ném lỗi như broker sập.</summary>
    private sealed class FakeBus : IIntegrationEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public string? FailWith { get; init; }

        public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct = default)
        {
            if (this.FailWith is not null)
            {
                throw new InvalidOperationException(this.FailWith);
            }

            this.Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
