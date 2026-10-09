// ============================================================================
// OrderPaymentFlowTests.cs — aggregate Order khi thanh toán đến SAU, từ Payment service (Buổi 55).
// Luật mới chỉ có 2: IsAwaitingPayment và RejectPayment(lý do). Mọi invariant cũ (b49) vẫn áp dụng.
// 👉 Bước 4 (b55.md)
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Tests.Domain;

public class OrderPaymentFlowTests
{
    private static Order AwaitingOrder()
    {
        Order order = Order.Create(new Customer("An", "0901234567"));
        order.AddItem(new Coffee("Cà phê sữa đá", 29000, "Robusta") { Id = 1 }, DrinkSize.S, 1);
        return order;
    }

    // Kiểm tra: đơn mới = đang chờ thanh toán; trả tiền xong (dù đến muộn) thì hết chờ và phát OrderPlaced như b50.
    [Fact]
    public void AwaitingPayment_UntilPaid()
    {
        Order order = AwaitingOrder();
        Assert.True(order.IsAwaitingPayment);

        order.Pay(PaymentFactory.Create(PaymentMethod.Card, 29000, "0901234567", "****1234"));

        Assert.False(order.IsAwaitingPayment);
        Assert.Single(order.DomainEvents.OfType<OrderPlaced>());
    }

    // Kiểm tra: thanh toán bị từ chối → Cancelled + OrderStatusChanged (Pending → Cancelled), hết "chờ thanh toán".
    [Fact]
    public void RejectPayment_CancelsAndRaisesStatusChanged()
    {
        Order order = AwaitingOrder();

        order.RejectPayment("Vượt hạn mức");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.False(order.IsAwaitingPayment);
        OrderStatusChanged changed = Assert.Single(order.DomainEvents.OfType<OrderStatusChanged>());
        Assert.Equal((OrderStatus.Pending, OrderStatus.Cancelled), (changed.From, changed.To));
    }

    // Kiểm tra: đơn ĐÃ trả tiền không bị hủy bởi 1 message "thất bại" đến trễ; lý do rỗng là lỗi lập trình.
    [Fact]
    public void RejectPayment_GuardsPaidOrderAndEmptyReason()
    {
        Order paid = AwaitingOrder();
        paid.Pay(new CashPayment(0, 100000));

        Assert.Throws<DomainException>(() => paid.RejectPayment("Thẻ bị từ chối"));
        Assert.Throws<ArgumentException>(() => AwaitingOrder().RejectPayment(" "));
        Assert.Equal(OrderStatus.Pending, paid.Status);
    }
}
