// ============================================================================
// CancelOrder.cs — hủy đơn (Buổi 51 · command).
// b48: luật "khách chỉ hủy khi Pending" là 1 khối if trong OrderService (title 409 "Không hủy được đơn").
// b49+: luật đó là của aggregate — Order.Cancel(requestedByCustomer) — handler chỉ truyền "ai đang hủy".
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
/// <summary>Hủy đơn: khách hủy đơn CỦA MÌNH khi Pending; nhân viên hủy được cả khi đang pha.</summary>
/// <param name="OrderId">Đơn cần hủy.</param>
/// <param name="User">Người gọi.</param>
public sealed record CancelOrderCommand(int OrderId, CurrentUser User) : ICommand<OrderDto>;

/// <summary>Hình dạng dữ liệu.</summary>
public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    /// <summary>Khai báo luật.</summary>
    public CancelOrderCommandValidator() => this.RuleFor(c => c.OrderId).GreaterThan(0);
}

/// <summary>Tải đơn → kiểm tra quyền → order.Cancel(...) → lưu.</summary>
public sealed class CancelOrderCommandHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelOrderCommand, OrderDto>
{
    /// <inheritdoc />
    public async Task<OrderDto> Handle(CancelOrderCommand command, CancellationToken ct)
    {
        Order? order = await orders.GetAsync(command.OrderId, ct);
        if (order is null || !command.User.CanAccess(order.OwnerId))
        {
            throw new NotFoundException($"Không có đơn {command.OrderId}"); // chống IDOR: 404 thay vì 403
        }

        // Khách (không phải nhân viên) → aggregate áp luật "chỉ khi Pending"
        order.Cancel(requestedByCustomer: !command.User.IsStaff);
        await unitOfWork.SaveChangesAsync(ct);
        return order.ToDto();
    }
}
