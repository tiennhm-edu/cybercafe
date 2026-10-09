// ============================================================================
// ChangeOrderStatus.cs — barista đổi trạng thái đơn (Buổi 51 · command).
// Handler chỉ 4 bước: tải aggregate → kiểm tra quyền xem → gọi method nghiệp vụ → lưu.
// Luật luồng trạng thái + "chưa trả tiền thì chưa pha" nằm trong Order (aggregate), KHÔNG ở đây.
// Báo realtime: Order raise OrderStatusChanged → NotifyOnOrderStatusChanged (sau khi lưu/commit).
// ============================================================================
using CyberCafe.Application.Common;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using FluentValidation;

namespace CyberCafe.Application.Orders.Commands;

// 👉 Bước 5 (b51.md)
/// <summary>Đổi trạng thái đơn (màn hình quầy).</summary>
/// <param name="OrderId">Đơn cần đổi.</param>
/// <param name="Status">Trạng thái đích.</param>
/// <param name="User">Người gọi (policy ProcessOrders ở Api đã chặn khách; kiểm tra lại cho chắc).</param>
public sealed record ChangeOrderStatusCommand(int OrderId, OrderStatus Status, CurrentUser User) : ICommand<OrderDto>;

/// <summary>Hình dạng dữ liệu: Id dương, trạng thái thuộc enum.</summary>
public sealed class ChangeOrderStatusCommandValidator : AbstractValidator<ChangeOrderStatusCommand>
{
    /// <summary>Khai báo luật.</summary>
    public ChangeOrderStatusCommandValidator()
    {
        this.RuleFor(c => c.OrderId).GreaterThan(0);
        // IsInEnum: chặn (OrderStatus)99 — JSON "Flying" đã bị Api từ chối, nhưng command có thể đến từ chỗ khác
        this.RuleFor(c => c.Status).IsInEnum().WithMessage("Trạng thái không hợp lệ");
    }
}

/// <summary>Tải đơn → order.ChangeStatus(...) → lưu.</summary>
public sealed class ChangeOrderStatusCommandHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeOrderStatusCommand, OrderDto>
{
    /// <inheritdoc />
    public async Task<OrderDto> Handle(ChangeOrderStatusCommand command, CancellationToken ct)
    {
        Order order = await orders.GetAsync(command.OrderId, ct) ?? throw new NotFoundException($"Không có đơn {command.OrderId}");
        if (!command.User.CanAccess(order.OwnerId))
        {
            throw new NotFoundException($"Không có đơn {command.OrderId}"); // không phải của bạn: giả như không tồn tại
        }

        order.ChangeStatus(command.Status);     // DomainException (sai luồng, chưa thanh toán) → 409
        await unitOfWork.SaveChangesAsync(ct);  // UPDATE Orders SET Status = ... → rồi phát OrderStatusChanged
        return order.ToDto();
    }
}
