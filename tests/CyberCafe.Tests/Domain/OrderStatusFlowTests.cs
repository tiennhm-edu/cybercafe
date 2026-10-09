// ============================================================================
// OrderStatusFlowTests.cs — luồng trạng thái đơn (Buổi 32–41 · state machine trong Domain; Buổi 49: aggregate).
// Test thuần C#: không cần Api, không cần database — luật nghiệp vụ test được độc lập.
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Tests.Domain;

public class OrderStatusFlowTests
{
    private static Order NewOrder()
    {
        Cart cart = new();
        cart.AddItem(new Coffee("Bạc xỉu", 32000, "Robusta") { Id = 2 });
        Order order = cart.ToOrder(new Customer("An", "0901234567"));
        order.Pay(new CashPayment(0, 32000)); // Buổi 49: invariant "chưa thanh toán thì chưa pha" → trả tiền trước
        return order;
    }

    // Kiểm tra: đi đúng luồng Pending → Preparing → Ready → Completed thành công.
    [Fact]
    public void ChangeStatus_HappyPath_ReachesCompleted()
    {
        Order order = NewOrder();

        order.ChangeStatus(OrderStatus.Preparing);
        order.ChangeStatus(OrderStatus.Ready);
        order.ChangeStatus(OrderStatus.Completed);

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    // Kiểm tra: các bước nhảy cóc / đi lùi / hủy đơn đã xong đều bị từ chối.
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Ready)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Pending)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Preparing)]
    public void CanChange_InvalidTransitions_ReturnFalse(OrderStatus from, OrderStatus to)
    {
        Assert.False(OrderStatusFlow.CanChange(from, to));
    }

    // Kiểm tra: ChangeStatus sai luồng ném DomainException (b49; vẫn là InvalidOperationException) và giữ nguyên trạng thái cũ.
    [Fact]
    public void ChangeStatus_Invalid_ThrowsAndKeepsStatus()
    {
        Order order = NewOrder();

        Assert.Throws<DomainException>(() => order.ChangeStatus(OrderStatus.Ready));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    // Kiểm tra: NextSteps trả đúng các nút cần hiện cho barista.
    [Fact]
    public void NextSteps_FromPending_AreStartOrCancel()
    {
        OrderStatus[] expected = [OrderStatus.Preparing, OrderStatus.Cancelled];
        Assert.Equal(expected, OrderStatusFlow.NextSteps(OrderStatus.Pending));
        Assert.Empty(OrderStatusFlow.NextSteps(OrderStatus.Completed));
    }

    // Kiểm tra: đơn giá được CHỐT lúc tạo dòng — đổi giá món sau đó không làm đổi đơn đã tạo.
    [Fact]
    public void OrderItem_SnapshotsUnitPrice()
    {
        Coffee coffee = new("Americano", 35000, "Arabica") { Id = 3 };
        Cart cart = new();
        cart.AddItem(coffee, DrinkSize.M, 1); // 40.000
        Order order = cart.ToOrder(new Customer("An", "0901234567"));

        coffee.UpdatePrice(50000);

        Assert.Equal(40000, order.FinalAmount.Amount);
    }
}
