// ============================================================================
// CartTests.cs — unit test cho Cart, OrderItem, giá theo size (Buổi 24–31 · Domain test; Buổi 50: tiền là Money).
// Domain là C# thuần nên test chỉ cần new Cart() — không cần chạy Blazor hay trình duyệt.
// Mỗi test theo mẫu AAA: Arrange (chuẩn bị) → Act (thực hiện) → Assert (kiểm tra).
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Tests.Domain;

public class CartTests
{
    // Dữ liệu dùng chung cho nhiều test: static readonly vì các test chỉ ĐỌC, không sửa các món này
    private static readonly Coffee CaPheSua = new("Cà phê sữa đá", 29000, "Robusta") { Id = 1 };
    private static readonly Tea TraDao = new("Trà đào cam sả", 39000, "Trà đen") { Id = 4 };
    private static readonly Cake Tiramisu = new("Bánh tiramisu", 35000, "Cà phê") { Id = 7 };

    // Kiểm tra: giỏ mới tạo thì trống và tổng = 0.
    [Fact]
    public void NewCart_IsEmpty_WithZeroTotal()
    {
        Cart cart = new();

        Assert.True(cart.IsEmpty);
        Assert.Equal(0, cart.Total);
    }

    // Kiểm tra (polymorphism): đồ uống cộng phụ thu theo size — S giá gốc, M +5.000, L +10.000.
    [Theory]
    [InlineData(DrinkSize.S, 29000)]
    [InlineData(DrinkSize.M, 34000)]
    [InlineData(DrinkSize.L, 39000)]
    public void Drink_PriceDependsOnSize(DrinkSize size, decimal expected)
    {
        Assert.Equal(expected, CaPheSua.GetPrice(size));
    }

    // Kiểm tra: bánh không có size → chọn L vẫn lưu thành S và tính giá gốc.
    [Fact]
    public void Cake_IgnoresSize()
    {
        Cart cart = new();

        OrderItem item = cart.AddItem(Tiramisu, DrinkSize.L, 1);

        Assert.Equal(DrinkSize.S, item.Size);
        Assert.Equal(35000, cart.Subtotal);
    }

    // Kiểm tra: thêm cùng món + cùng size → gộp vào 1 dòng, cộng dồn số lượng.
    [Fact]
    public void AddItem_SameProductAndSize_MergesQuantity()
    {
        Cart cart = new();

        cart.AddItem(CaPheSua, DrinkSize.M, 1);
        cart.AddItem(CaPheSua, DrinkSize.M, 2);

        Assert.Single(cart.Items);
        Assert.Equal(3, cart.ItemCount);
    }

    // Kiểm tra: cùng món nhưng khác size → tạo dòng mới.
    [Fact]
    public void AddItem_DifferentSize_CreatesNewLine()
    {
        Cart cart = new();

        cart.AddItem(CaPheSua, DrinkSize.S, 1);
        cart.AddItem(CaPheSua, DrinkSize.L, 1);

        Assert.Equal(2, cart.Items.Count);
    }

    // Kiểm tra: tạm tính = tổng (đơn giá theo size × số lượng) của mọi dòng.
    [Fact]
    public void Subtotal_SumsSizePriceTimesQuantity()
    {
        Cart cart = new();

        cart.AddItem(CaPheSua, DrinkSize.M, 2); // 34.000 x 2 = 68.000
        cart.AddItem(TraDao, DrinkSize.L, 1);   // 49.000
        cart.AddItem(Tiramisu, DrinkSize.S, 1); // 35.000

        Assert.Equal(152000, cart.Subtotal);
        Assert.Equal(4, cart.ItemCount);
    }

    // Kiểm tra: đặt số lượng về 0 → dòng bị xóa khỏi giỏ.
    [Fact]
    public void UpdateQuantity_ToZero_RemovesLine()
    {
        Cart cart = new();
        OrderItem item = cart.AddItem(CaPheSua, DrinkSize.S, 2);

        cart.UpdateQuantity(item, 0);

        Assert.True(cart.IsEmpty);
    }

    // Kiểm tra: đổi số lượng → tổng tiền tính lại đúng.
    [Fact]
    public void UpdateQuantity_ChangesTotal()
    {
        Cart cart = new();
        OrderItem item = cart.AddItem(TraDao, DrinkSize.S, 1);

        cart.UpdateQuantity(item, 3);

        Assert.Equal(117000, cart.Total);
    }

    // Kiểm tra: thêm món tạm hết → ném InvalidOperationException.
    [Fact]
    public void AddItem_UnavailableProduct_Throws()
    {
        Cart cart = new();
        Tea matcha = new("Matcha latte", 45000, "Matcha") { Id = 6, IsAvailable = false };

        Assert.Throws<InvalidOperationException>(() => cart.AddItem(matcha, DrinkSize.S, 1));
    }

    // Kiểm tra: giảm giá thành viên 10% được trừ đúng vào tổng.
    [Fact]
    public void Total_AppliesMemberDiscountPercent()
    {
        Cart cart = new();
        cart.AddItem(TraDao, DrinkSize.L, 2); // 98.000

        cart.ApplyDiscount(new MemberDiscount("Thành viên", 10));

        Assert.Equal(9800, cart.DiscountAmount);
        Assert.Equal(88200, cart.Total);
    }

    // Kiểm tra: voucher trừ số tiền cố định vào tổng.
    [Fact]
    public void Total_AppliesVoucher()
    {
        Cart cart = new();
        cart.AddItem(CaPheSua, DrinkSize.S, 2); // 58.000

        cart.ApplyDiscount(new VoucherDiscount("Voucher 20K", "GIAM20K", 20000));

        Assert.Equal(38000, cart.Total);
    }

    // Kiểm tra: Clear() xóa cả món lẫn giảm giá.
    [Fact]
    public void Clear_RemovesItemsAndDiscount()
    {
        Cart cart = new();
        cart.AddItem(CaPheSua);
        cart.ApplyDiscount(new MemberDiscount("Thành viên", 10));

        cart.Clear();

        Assert.True(cart.IsEmpty);
        Assert.Null(cart.Discount);
    }

    // Kiểm tra: chốt đơn từ giỏ trống → ném InvalidOperationException.
    [Fact]
    public void ToOrder_EmptyCart_Throws()
    {
        Cart cart = new();

        Assert.Throws<InvalidOperationException>(() => cart.ToOrder(new Customer("An", "0901234567")));
    }

    // Kiểm tra: Order sao chép các dòng — sửa giỏ sau khi đặt không làm đổi đơn đã chốt.
    [Fact]
    public void ToOrder_CopiesItems_SoLaterCartChangesDoNotAffectOrder()
    {
        Cart cart = new();
        OrderItem item = cart.AddItem(CaPheSua, DrinkSize.S, 1);
        cart.ApplyDiscount(new VoucherDiscount("Voucher 20K", "GIAM20K", 20000));

        Order order = cart.ToOrder(new Customer("An", "0901234567"), "Ít đá");
        cart.UpdateQuantity(item, 5);

        Assert.Equal(1, order.ItemCount);
        Assert.Equal(9000, order.FinalAmount.Amount); // Money (b50) → so phần decimal
        Assert.Equal("Ít đá", order.Note);
    }
}
