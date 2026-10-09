// ============================================================================
// CartStateAndCheckoutTests.cs — test các class của project Web (Buổi 24–31).
// CartState, DiscountCatalog, CheckoutModel, PaymentFactory đều là class C# thường
// → test trực tiếp, không cần render component.
// Buổi 32–41: MenuService/OrderStore/DiscountService (in-memory) đã bỏ — test tương ứng chuyển sang
// DiscountCatalog (Domain), CheckoutModel.ToRequest (form → API) và PaymentFactory (Domain).
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;
using CyberCafe.Web.Models;
using CyberCafe.Web.State;

namespace CyberCafe.Tests.Web;

public class CartStateAndCheckoutTests
{
    // Món mẫu (trước đây lấy từ MenuService in-memory; giờ menu nằm ở Api nên test tự tạo)
    private static readonly Coffee CaPheSua = new("Cà phê sữa đá", 29000, "Robusta") { Id = 1 };
    private static readonly Cake Tiramisu = new("Bánh tiramisu", 35000, "Cà phê") { Id = 7 };

    // Kiểm tra: mỗi thao tác đổi giỏ qua CartState bắn OnChange đúng 1 lần (AddItem + Clear = 2 lần).
    [Fact]
    public void CartState_RaisesOnChange_WhenItemAdded()
    {
        CartState state = new();
        int raised = 0;
        state.OnChange += () => raised++;

        state.AddItem(CaPheSua, DrinkSize.M, 1);
        state.Clear();

        Assert.Equal(2, raised);
    }

    // Kiểm tra: mã hợp lệ (không phân biệt hoa/thường, có khoảng trắng) tìm được; mã lạ trả về null.
    [Fact]
    public void DiscountCatalog_KnownAndUnknownCodes()
    {
        Assert.NotNull(DiscountCatalog.FindByCode(" giam20k "));
        Assert.NotNull(DiscountCatalog.FindByCode("MEMBER10"));
        Assert.Null(DiscountCatalog.FindByCode("FREE"));
    }

    // Kiểm tra: CartState nhớ MÃ đã chuẩn hóa (để gửi API), mã sai thì giỏ giữ nguyên, Clear xóa luôn mã.
    [Fact]
    public void CartState_ApplyDiscountCode_RemembersNormalizedCode()
    {
        CartState state = new();
        state.AddItem(CaPheSua, DrinkSize.S, 2);

        Assert.False(state.ApplyDiscountCode("FREE"));
        Assert.Null(state.Cart.Discount);

        Assert.True(state.ApplyDiscountCode(" giam20k "));
        Assert.Equal("GIAM20K", state.DiscountCode);

        state.Clear();
        Assert.Null(state.DiscountCode);
    }

    // Kiểm tra: ToRequest chỉ gửi Id món + size + số lượng + mã giảm giá (không gửi giá), bỏ số thẻ khi trả tiền mặt.
    [Fact]
    public void CheckoutModel_ToRequest_MapsCartLinesWithoutPrices()
    {
        Cart cart = new();
        cart.AddItem(CaPheSua, DrinkSize.L, 2);
        cart.AddItem(Tiramisu, DrinkSize.L, 1); // bánh → size S
        CheckoutModel model = new() { FullName = " An ", PhoneNumber = "0901234567", CardNumber = "411111111111", Note = "Ít đá" };

        PlaceOrderRequest request = model.ToRequest(cart, "GIAM20K");

        Assert.Equal("An", request.CustomerName);
        // record so sánh theo GIÁ TRỊ → Assert.Equal so được từng dòng
        OrderLineRequest[] expected = [new(1, DrinkSize.L, 2), new(7, DrinkSize.S, 1)];
        Assert.Equal(expected, request.Items);
        Assert.Equal("GIAM20K", request.DiscountCode);
        Assert.Equal(PaymentMethod.Cash, request.PaymentMethod);
        Assert.Null(request.CardNumber);
    }

    // Hàm trợ giúp: chạy validation DataAnnotations giống EditForm, trả về danh sách lỗi.
    // validateAllProperties: true → kiểm tra mọi attribute, không chỉ [Required].
    private static List<ValidationResult> Validate(CheckoutModel model)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    // Kiểm tra: regex SĐT trên form chấp nhận 10–11 chữ số bắt đầu bằng 0, từ chối trường hợp khác.
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("09012345678", true)]
    [InlineData("090123456", false)]
    [InlineData("1901234567", false)]
    public void CheckoutModel_PhoneValidation(string phone, bool valid)
    {
        CheckoutModel model = new() { FullName = "Nguyễn Văn An", PhoneNumber = phone };

        Assert.Equal(valid, Validate(model).Count == 0);
    }

    // Kiểm tra: chọn thanh toán thẻ mà không nhập số thẻ → lỗi gắn với field CardNumber.
    [Fact]
    public void CheckoutModel_CardWithoutNumber_IsInvalid()
    {
        CheckoutModel model = new() { FullName = "An", PhoneNumber = "0901234567", PaymentMethod = PaymentMethod.Card };

        // DataAnnotations hợp lệ → mới chạy IValidatableObject.Validate
        Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(CheckoutModel.CardNumber)));
    }

    // Kiểm tra: PaymentFactory (trước là CheckoutModel.CreatePayment) trả về đúng class con Payment theo lựa chọn.
    [Theory]
    [InlineData(PaymentMethod.Cash, typeof(CashPayment))]
    [InlineData(PaymentMethod.Card, typeof(CardPayment))]
    [InlineData(PaymentMethod.Momo, typeof(MomoPayment))]
    public void PaymentFactory_CreatesMatchingPayment(PaymentMethod method, Type expected)
    {
        Assert.IsType(expected, PaymentFactory.Create(method, 50000, "0901234567", "411111111111"));
    }

    // Kiểm tra: thẻ chỉ được giữ 4 số cuối (không lưu số thẻ đầy đủ xuống DB).
    [Fact]
    public void PaymentFactory_Card_KeepsOnlyLastFourDigits()
    {
        CardPayment card = Assert.IsType<CardPayment>(PaymentFactory.Create(PaymentMethod.Card, 50000, "0901234567", "4111111111111234"));

        Assert.Equal("****1234", card.CardNumber);
        Assert.Equal("****1234", card.MaskedCardNumber);
    }
}
