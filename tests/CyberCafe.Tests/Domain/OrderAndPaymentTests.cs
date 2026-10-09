// ============================================================================
// OrderAndPaymentTests.cs — unit test cho Order, Payment, Person (Buổi 24–31 · Domain test).
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Tests.Domain;

public class OrderAndPaymentTests
{
    // Hàm trợ giúp (không phải test): tạo nhanh 1 đơn Americano với số lượng/giảm giá tùy chọn.
    private static Order CreateOrder(decimal unitPrice, int quantity, Discount? discount = null)
    {
        Coffee coffee = new("Americano", unitPrice, "Arabica") { Id = 3 };
        Cart cart = new();
        cart.AddItem(coffee, DrinkSize.S, quantity);
        if (discount is not null)
        {
            cart.ApplyDiscount(discount);
        }

        return cart.ToOrder(new Customer("Bình", "0912345678"));
    }

    // Kiểm tra: Checkout gán Amount của payment = FinalAmount (sau giảm giá), đánh dấu đã trả và lưu payment vào đơn.
    [Fact]
    public void Checkout_SyncsPaymentAmountWithFinalAmount()
    {
        Order order = CreateOrder(35000, 2, new VoucherDiscount("V", "V20K", 20000)); // 70k - 20k
        CardPayment payment = new(0, "4111111111111111", "VCB");

        order.Checkout(payment);

        Assert.Equal(50000, payment.Amount);
        Assert.True(payment.IsPaid);
        Assert.Same(payment, order.Payment);
    }

    // Kiểm tra: thanh toán xong cộng điểm cho khách, 10.000 đ = 1 điểm.
    [Fact]
    public void Checkout_AddsLoyaltyPoint_Per10k()
    {
        Order order = CreateOrder(35000, 2); // 70.000 → 7 điểm

        order.Checkout(new CashPayment(70000, 70000));

        Assert.Equal(7, order.Customer.LoyaltyPoints);
    }

    // Kiểm tra: khách đưa thiếu tiền → Process() ném exception và IsPaid vẫn false.
    [Fact]
    public void CashPayment_NotEnoughCash_Throws()
    {
        CashPayment payment = new(50000, 20000);

        Assert.Throws<InvalidOperationException>(() => payment.Process());
        Assert.False(payment.IsPaid);
    }

    // Kiểm tra: tiền thừa = tiền khách đưa − số tiền phải trả.
    [Fact]
    public void CashPayment_ComputesChange()
    {
        CashPayment payment = new(45000, 50000);

        payment.Process();

        Assert.Equal(5000, payment.Change);
    }

    // Kiểm tra: số thẻ chỉ hiện 4 số cuối.
    [Fact]
    public void CardPayment_MasksCardNumber()
    {
        Assert.Equal("****1111", new CardPayment(1000, "4111111111111111", "VCB").MaskedCardNumber);
    }

    // Kiểm tra: quy tắc SĐT — 10–11 chữ số, bắt đầu bằng 0, chỉ gồm chữ số.
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("09012345678", true)]
    [InlineData("901234567", false)]
    [InlineData("090123456", false)]
    [InlineData("090123456789", false)]
    [InlineData("09012a4567", false)]
    public void Person_PhoneNumberRule_10To11DigitsStartingWith0(string phone, bool expected)
    {
        Assert.Equal(expected, Person.IsValidPhoneNumber(phone));
    }
}
