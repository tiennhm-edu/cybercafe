// ============================================================================
// PaymentResultConsumers.cs — Order service NGHE kết quả thanh toán từ RabbitMQ (Buổi 55 · consumer).
// Consumer = "cửa vào" kiểu mới, ngang hàng controller (HTTP) và hub (SignalR): chỉ dịch message → command → ISender.
// Mọi luật (idempotent, aggregate, transaction) nằm ở Application — test được không cần RabbitMQ.
// MassTransit tạo 1 DI scope cho MỖI message → DbContext, ISender... là bản riêng của message đó (như 1 request HTTP).
// Lỗi ném ra → MassTransit retry (UseMessageRetry) → vẫn lỗi → chuyển sang queue "..._error" để người xem xét.
// ============================================================================
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Orders.Commands;
using CyberCafe.IntegrationEvents;
using MassTransit;

namespace CyberCafe.Infrastructure.Messaging;

// 👉 Bước 4 (b55.md)
/// <summary>PaymentCompleted → ConfirmOrderPaymentCommand.</summary>
public sealed class PaymentCompletedConsumer(ISender sender) : IConsumer<PaymentCompletedIntegrationEvent>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> context)
    {
        PaymentCompletedIntegrationEvent m = context.Message;
        return sender.Send(
            new ConfirmOrderPaymentCommand(m.EventId, m.OrderId, m.Amount, m.PaymentMethod, m.CardLast4, m.TransactionId),
            context.CancellationToken);
    }
}

/// <summary>PaymentFailed → RejectOrderPaymentCommand.</summary>
public sealed class PaymentFailedConsumer(ISender sender) : IConsumer<PaymentFailedIntegrationEvent>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<PaymentFailedIntegrationEvent> context)
    {
        PaymentFailedIntegrationEvent m = context.Message;
        return sender.Send(new RejectOrderPaymentCommand(m.EventId, m.OrderId, m.Reason), context.CancellationToken);
    }
}
