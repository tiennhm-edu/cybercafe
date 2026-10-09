// ============================================================================
// PaymentResults.cs — Order service nhận KẾT QUẢ thanh toán từ Payment service (Buổi 55 · idempotent consumer).
// Ai gửi 2 command này? Consumer MassTransit ở Infrastructure/Messaging (PaymentCompleted/FailedConsumer).
// Consumer chỉ là "adapter" mỏng (giống controller): đổi message → command → ISender.Send(...).
// Nhờ vậy vẫn đi qua pipeline b52: Logging → Validation → Transaction → Handler, và test được không cần broker.
// IDEMPOTENT: mỗi handler hỏi IInbox "EventId này xử lý chưa?" → rồi mới làm → đánh dấu đã xử lý, CÙNG SaveChanges.
// ============================================================================
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Orders.Commands;

/// <summary>Kết quả xử lý 1 message kết quả thanh toán.</summary>
public enum PaymentResultOutcome
{
    /// <summary>Đã cập nhật đơn.</summary>
    Applied,

    /// <summary>Message trùng (EventId đã xử lý) → bỏ qua, không làm gì.</summary>
    Duplicate,

    /// <summary>Không áp dụng được (đơn không tồn tại / đã hủy / đã thanh toán) → ghi log, đánh dấu đã xử lý.</summary>
    Ignored,
}

// 👉 Bước 4 (b55.md)
/// <summary>Payment báo đã thu tiền → <c>order.Pay(...)</c>.</summary>
/// <param name="EventId">EventId của PaymentCompletedIntegrationEvent (khóa chống trùng).</param>
/// <param name="OrderId">Đơn đã thanh toán.</param>
/// <param name="Amount">Số tiền Payment đã thu.</param>
/// <param name="PaymentMethod">"Cash" / "Card" / "Momo".</param>
/// <param name="CardLast4">4 số cuối thẻ (nếu Card).</param>
/// <param name="TransactionId">Mã giao dịch bên Payment.</param>
public sealed record ConfirmOrderPaymentCommand(
    Guid EventId, int OrderId, decimal Amount, string PaymentMethod, string? CardLast4, string TransactionId)
    : ICommand<PaymentResultOutcome>;

/// <summary>Payment từ chối → hủy đơn.</summary>
/// <param name="EventId">EventId của PaymentFailedIntegrationEvent.</param>
/// <param name="OrderId">Đơn bị từ chối.</param>
/// <param name="Reason">Lý do.</param>
public sealed record RejectOrderPaymentCommand(Guid EventId, int OrderId, string Reason) : ICommand<PaymentResultOutcome>;

/// <summary>Hình dạng dữ liệu — message từ service khác cũng phải được kiểm tra như request HTTP.</summary>
public sealed class ConfirmOrderPaymentCommandValidator : AbstractValidator<ConfirmOrderPaymentCommand>
{
    /// <summary>Khai báo luật.</summary>
    public ConfirmOrderPaymentCommandValidator()
    {
        this.RuleFor(c => c.EventId).NotEmpty();
        this.RuleFor(c => c.OrderId).GreaterThan(0);
        this.RuleFor(c => c.Amount).GreaterThanOrEqualTo(0);
        this.RuleFor(c => c.PaymentMethod).Must(m => Enum.TryParse<PaymentMethod>(m, ignoreCase: true, out _))
            .WithMessage("Hình thức thanh toán không hợp lệ");
    }
}

/// <summary>Hình dạng dữ liệu.</summary>
public sealed class RejectOrderPaymentCommandValidator : AbstractValidator<RejectOrderPaymentCommand>
{
    /// <summary>Khai báo luật.</summary>
    public RejectOrderPaymentCommandValidator()
    {
        this.RuleFor(c => c.EventId).NotEmpty();
        this.RuleFor(c => c.OrderId).GreaterThan(0);
        this.RuleFor(c => c.Reason).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Áp kết quả "đã thanh toán" vào aggregate, đúng 1 lần cho mỗi EventId.</summary>
public sealed class ConfirmOrderPaymentCommandHandler(
    IOrderRepository orders,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    ILogger<ConfirmOrderPaymentCommandHandler> logger) : ICommandHandler<ConfirmOrderPaymentCommand, PaymentResultOutcome>
{
    // Tên "consumer" trong bảng ProcessedMessages: cùng 1 EventId có thể được NHIỀU consumer khác nhau xử lý
    private const string Consumer = nameof(ConfirmOrderPaymentCommand);

    /// <inheritdoc />
    public async Task<PaymentResultOutcome> Handle(ConfirmOrderPaymentCommand command, CancellationToken ct)
    {
        // 👉 Bước 5 (b55.md): message trùng → dừng NGAY, trước khi chạm vào aggregate
        if (await inbox.HasProcessedAsync(command.EventId, Consumer, ct))
        {
            return PaymentResultOutcome.Duplicate;
        }

        // Tải NGUYÊN aggregate (phía GHI — b53): Pay() cần Items, Discount để chốt số tiền và cộng điểm
        PaymentResultOutcome outcome = PaymentResultOutcome.Applied;
        Order? order = await orders.GetAsync(command.OrderId, ct);
        if (order is null || !order.IsAwaitingPayment)
        {
            // Khách đã tự hủy trong lúc chờ (hoặc đơn đã trả rồi) → không Pay được nữa.
            // Thực tế: phát RefundRequested cho Payment hoàn tiền (bù trừ — "compensation"). Bài tập b55.
            logger.LogWarning("Bỏ qua PaymentCompleted cho đơn {OrderId}: không còn chờ thanh toán (cần hoàn tiền {TransactionId})",
                command.OrderId, command.TransactionId);
            outcome = PaymentResultOutcome.Ignored;
        }
        else if (order.FinalAmount.Amount != command.Amount)
        {
            // Không tin mù quáng message: số tiền phải khớp số aggregate đã tính
            logger.LogWarning("Số tiền thanh toán {Paid} không khớp đơn {OrderId} ({Expected}) → hủy đơn",
                command.Amount, command.OrderId, order.FinalAmount.Amount);
            order.RejectPayment("Số tiền thanh toán không khớp");
        }
        else
        {
            PaymentMethod method = Enum.Parse<PaymentMethod>(command.PaymentMethod, ignoreCase: true);
            // Thẻ: PaymentFactory chỉ giữ 4 số cuối → truyền "****1234" là đủ (Order service không bao giờ thấy số thẻ đầy đủ)
            string? card = method == PaymentMethod.Card ? "****" + (command.CardLast4 ?? "0000") : null;
            // Pay: Process() đa hình + cộng điểm + Raise(OrderPaid, OrderPlaced) — y hệt b53, chỉ là đến muộn hơn
            order.Pay(PaymentFactory.Create(method, command.Amount, order.Customer.PhoneNumber, card));
        }

        // Đánh dấu + lưu CÙNG 1 SaveChanges (và cùng transaction của TransactionBehavior) với thay đổi của đơn.
        inbox.MarkProcessed(command.EventId, Consumer);
        await unitOfWork.SaveChangesAsync(ct);
        return outcome;
    }
}

/// <summary>Áp kết quả "thanh toán thất bại" → hủy đơn, đúng 1 lần cho mỗi EventId.</summary>
public sealed class RejectOrderPaymentCommandHandler(
    IOrderRepository orders,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    ILogger<RejectOrderPaymentCommandHandler> logger) : ICommandHandler<RejectOrderPaymentCommand, PaymentResultOutcome>
{
    private const string Consumer = nameof(RejectOrderPaymentCommand);

    /// <inheritdoc />
    public async Task<PaymentResultOutcome> Handle(RejectOrderPaymentCommand command, CancellationToken ct)
    {
        if (await inbox.HasProcessedAsync(command.EventId, Consumer, ct))
        {
            return PaymentResultOutcome.Duplicate;
        }

        PaymentResultOutcome outcome = PaymentResultOutcome.Applied;
        Order? order = await orders.GetAsync(command.OrderId, ct);
        if (order is null || !order.IsAwaitingPayment)
        {
            logger.LogWarning("Bỏ qua PaymentFailed cho đơn {OrderId}: không còn chờ thanh toán", command.OrderId);
            outcome = PaymentResultOutcome.Ignored;
        }
        else
        {
            // Lý do chỉ ghi log (chưa có cột) — muốn hiện cho khách: thêm cột CancellationReason (bài tập)
            // RejectPayment → Cancelled → OrderStatusChanged → NotifyOnOrderStatusChanged (b50) báo khách qua SignalR
            order.RejectPayment(command.Reason);
            logger.LogInformation("Đơn {Code} bị hủy do thanh toán thất bại: {Reason}", order.Code, command.Reason);
        }

        inbox.MarkProcessed(command.EventId, Consumer);
        await unitOfWork.SaveChangesAsync(ct);
        return outcome;
    }
}
