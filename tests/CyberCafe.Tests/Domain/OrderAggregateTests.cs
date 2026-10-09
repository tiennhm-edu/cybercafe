// ============================================================================
// OrderAggregateTests.cs — INVARIANT của aggregate Order (Buổi 49 · DDD; Buổi 50 · domain event).
// Test thuần C#: không database, không Api. Mỗi test kiểm 1 luật mà aggregate PHẢI tự bảo vệ —
// dù ai gọi (controller, hub, job, test) cũng không vượt qua được.
// 👉 Bước 6 (b49.md)
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Tests.Domain;

public class OrderAggregateTests
{
    private static readonly Coffee CaPheSua = new("Cà phê sữa đá", 29000, "Robusta") { Id = 1 };
    private static readonly Cake Tiramisu = new("Bánh tiramisu", 35000, "Cà phê") { Id = 7 };

    // Đơn 1 ly cà phê sữa size S (29.000 đ), chưa thanh toán
    private static Order NewOrder(int? ownerId = 5)
    {
        Order order = Order.Create(new Customer("An", "0901234567"), "Ít đá", ownerId);
        order.AddItem(CaPheSua, DrinkSize.S, 1);
        return order;
    }

    // Đơn đã thanh toán tiền mặt đủ (sẵn sàng cho barista)
    private static Order PaidOrder()
    {
        Order order = NewOrder();
        order.Pay(new CashPayment(0, 100000));
        return order;
    }

    // Kiểm tra: đơn mới tạo ở Pending, chưa thanh toán, chưa có event nào, ghi chú được Trim.
    [Fact]
    public void Create_StartsPendingUnpaid_WithoutEvents()
    {
        Order order = Order.Create(new Customer("An", "0901234567"), "  Ít đá  ", ownerId: 5);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.False(order.IsPaid);
        Assert.Empty(order.DomainEvents);
        Assert.Equal("Ít đá", order.Note);
        Assert.Equal(5, order.OwnerId);
    }

    // Kiểm tra: cùng món + cùng size thì cộng dồn 1 dòng; bánh luôn là size S dù chọn L.
    [Fact]
    public void AddItem_MergesSameProductAndSize_CakeIgnoresSize()
    {
        Order order = NewOrder();

        order.AddItem(CaPheSua, DrinkSize.S, 2);
        order.AddItem(Tiramisu, DrinkSize.L, 1);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(3, order.Items[0].Quantity);
        Assert.Equal(DrinkSize.S, order.Items[1].Size);
        Assert.Equal(new Money(29000 * 3 + 35000), order.TotalAmount); // so sánh value object bằng GIÁ TRỊ
    }

    // Kiểm tra: món tạm hết → DomainException (→ 409), đơn không đổi.
    [Fact]
    public void AddItem_UnavailableProduct_Throws()
    {
        Order order = NewOrder();
        Tea matcha = new("Matcha latte", 45000, "Matcha") { Id = 6, IsAvailable = false };

        DomainException ex = Assert.Throws<DomainException>(() => order.AddItem(matcha));

        Assert.Contains("tạm hết", ex.Message);
        Assert.Single(order.Items);
    }

    // Kiểm tra (invariant 1): đã thanh toán thì không thêm món / đổi giảm giá được nữa.
    [Fact]
    public void AfterPay_AddItemAndApplyDiscount_AreRejected()
    {
        Order order = PaidOrder();

        Assert.Throws<DomainException>(() => order.AddItem(Tiramisu));
        Assert.Throws<DomainException>(() => order.ApplyDiscount(new MemberDiscount("Thành viên", 10)));
        Assert.Equal(new Money(29000), order.FinalAmount);
    }

    // Kiểm tra (invariant 2): không thanh toán đơn rỗng; không thanh toán 2 lần.
    [Fact]
    public void Pay_EmptyOrTwice_IsRejected()
    {
        Order empty = Order.Create(new Customer("An", "0901234567"));
        Order paid = PaidOrder();

        Assert.Throws<DomainException>(() => empty.Pay(new CashPayment(0, 0)));
        Assert.Throws<DomainException>(() => paid.Pay(new CashPayment(0, 100000)));
    }

    // Kiểm tra (domain event): Pay phát OrderPaid (kèm số tiền) và OrderPlaced; payment nhận đúng số tiền sau giảm.
    [Fact]
    public void Pay_RaisesOrderPaidAndOrderPlaced()
    {
        Order order = NewOrder();
        order.AddItem(CaPheSua, DrinkSize.S, 1); // 2 × 29.000 = 58.000
        order.ApplyDiscount(new VoucherDiscount("Voucher 20K", "GIAM20K", 20000));
        CardPayment card = new(0, "****1234", "Visa");

        order.Pay(card);

        Assert.Equal(38000, card.Amount);
        OrderPaid paid = Assert.Single(order.DomainEvents.OfType<OrderPaid>());
        Assert.Equal(new Money(38000), paid.Amount);
        Assert.Same(order, Assert.Single(order.DomainEvents.OfType<OrderPlaced>()).Order);
        Assert.Equal(3, order.Customer.LoyaltyPoints); // 38.000 / 10.000
    }

    // Kiểm tra (invariant 3): chưa thanh toán thì barista chưa pha được.
    [Fact]
    public void StartPreparing_Unpaid_Throws()
    {
        Order order = NewOrder();

        DomainException ex = Assert.Throws<DomainException>(order.StartPreparing);

        Assert.Contains("chưa thanh toán", ex.Message);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    // Kiểm tra: đi hết vòng đời, mỗi bước phát 1 OrderStatusChanged có From → To đúng.
    [Fact]
    public void Lifecycle_RaisesStatusChangedForEachStep()
    {
        Order order = PaidOrder();
        order.ClearDomainEvents(); // bỏ OrderPaid/OrderPlaced để chỉ đếm event đổi trạng thái

        order.StartPreparing();
        order.MarkReady();
        order.Complete();

        (OrderStatus, OrderStatus)[] steps = order.DomainEvents.OfType<OrderStatusChanged>().Select(e => (e.From, e.To)).ToArray();
        Assert.Equal([(OrderStatus.Pending, OrderStatus.Preparing), (OrderStatus.Preparing, OrderStatus.Ready), (OrderStatus.Ready, OrderStatus.Completed)], steps);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    // Kiểm tra (invariant 4): khách chỉ tự hủy khi Pending; nhân viên hủy được khi đang pha; Ready thì không ai hủy được.
    [Fact]
    public void Cancel_RulesForCustomerAndStaff()
    {
        Order pending = PaidOrder();
        Order preparing = PaidOrder();
        preparing.StartPreparing();
        Order ready = PaidOrder();
        ready.StartPreparing();
        ready.MarkReady();

        pending.Cancel(requestedByCustomer: true);
        Assert.Throws<DomainException>(() => preparing.Cancel(requestedByCustomer: true));
        preparing.Cancel(requestedByCustomer: false);
        Assert.Throws<DomainException>(() => ready.Cancel());

        Assert.Equal((OrderStatus.Cancelled, OrderStatus.Cancelled, OrderStatus.Ready), (pending.Status, preparing.Status, ready.Status));
    }

    // Kiểm tra: ChangeStatus (nút barista) dịch đúng sang method nghiệp vụ; "quay lại Pending" luôn bị từ chối.
    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Completed)]
    public void ChangeStatus_FromPending_OnlyPreparingOrCancelAllowed(OrderStatus target)
    {
        Order order = PaidOrder();

        Assert.Throws<DomainException>(() => order.ChangeStatus(target));
        order.ChangeStatus(OrderStatus.Preparing);
        Assert.Equal(OrderStatus.Preparing, order.Status);
    }

    // Kiểm tra: đã hủy thì không thanh toán / sửa được nữa (đơn không còn là "nháp").
    [Fact]
    public void CancelledOrder_CannotBeEdited()
    {
        Order order = NewOrder();
        order.Cancel();

        Assert.Throws<DomainException>(() => order.AddItem(Tiramisu));
        Assert.Throws<DomainException>(() => order.Pay(new CashPayment(0, 100000)));
    }

    // Kiểm tra: chủ đơn so theo OwnerId; đơn không chủ (dữ liệu cũ) thì không thuộc về ai, kể cả "null".
    [Fact]
    public void IsOwnedBy_ComparesOwnerId()
    {
        Assert.True(NewOrder(ownerId: 5).IsOwnedBy(5));
        Assert.False(NewOrder(ownerId: 5).IsOwnedBy(6));
        Assert.False(NewOrder(ownerId: null).IsOwnedBy(null));
    }

    // Kiểm tra: ClearDomainEvents xóa sạch (DbContext gọi sau khi lấy event ra để không phát lặp).
    [Fact]
    public void ClearDomainEvents_EmptiesList()
    {
        Order order = PaidOrder();

        order.ClearDomainEvents();

        Assert.Empty(order.DomainEvents);
    }
}
