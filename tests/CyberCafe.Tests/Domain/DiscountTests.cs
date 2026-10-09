// ============================================================================
// DiscountTests.cs — unit test cho các loại giảm giá (Buổi 24–31 · Polymorphism).
// ============================================================================
using CyberCafe.Domain.Discounts;

namespace CyberCafe.Tests.Domain;

public class DiscountTests
{
    // Kiểm tra: Discount gốc (class cha) không giảm gì.
    [Fact]
    public void BaseDiscount_DoesNotChangeAmount()
    {
        Assert.Equal(50000, new Discount("Không giảm").Apply(50000));
    }

    // Kiểm tra: giảm 15% trên 100.000 → còn 85.000, số tiền giảm 15.000.
    [Fact]
    public void MemberDiscount_ReducesByPercent()
    {
        MemberDiscount discount = new("Thành viên", 15);

        Assert.Equal(85000, discount.Apply(100000));
        Assert.Equal(15000, discount.GetDiscountAmount(100000));
    }

    // Kiểm tra: phần trăm ngoài khoảng 0–100 → constructor ném ArgumentException.
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void MemberDiscount_InvalidPercent_Throws(decimal percent)
    {
        Assert.Throws<ArgumentException>(() => new MemberDiscount("Sai", percent));
    }

    // Kiểm tra: voucher lớn hơn tổng tiền → kết quả 0 (không âm) và mã được chuẩn hóa thành chữ HOA.
    [Fact]
    public void VoucherDiscount_NeverBelowZero()
    {
        VoucherDiscount voucher = new("Voucher 20K", "giam20k", 20000);

        Assert.Equal(0, voucher.Apply(15000));
        Assert.Equal("GIAM20K", voucher.Code);
    }

    // Kiểm tra (polymorphism): List<Discount> chứa nhiều loại, gọi Apply() thì mỗi object chạy quy tắc của riêng nó.
    [Fact]
    public void Polymorphism_ListOfDiscounts_EachAppliesOwnRule()
    {
        List<Discount> discounts =
        [
            new MemberDiscount("Thành viên", 10),
            new VoucherDiscount("Voucher", "V10K", 10000)
        ];

        decimal[] results = discounts.Select(d => d.Apply(100000)).ToArray();

        Assert.Equal([90000m, 90000m], results);
    }

    // Kiểm tra: số tiền gốc âm → ném ArgumentException.
    [Fact]
    public void Apply_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MemberDiscount("Thành viên", 10).Apply(-1));
    }
}
