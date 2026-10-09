// ============================================================================
// CartStateAndCheckoutTests.cs — test các class của project Web (Buổi 24–31).
// CartState, DiscountService, OrderStore, CheckoutModel đều là class C# thường
// → test trực tiếp, không cần render component.
// ============================================================================
using System.ComponentModel.DataAnnotations;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using CyberCafe.Web.Models;
using CyberCafe.Web.Services;
using CyberCafe.Web.State;

namespace CyberCafe.Tests.Web;

public class CartStateAndCheckoutTests
{
    private readonly MenuService _menu = new();

    // Kiểm tra: mỗi thao tác đổi giỏ qua CartState bắn OnChange đúng 1 lần (AddItem + Clear = 2 lần).
    [Fact]
    public void CartState_RaisesOnChange_WhenItemAdded()
    {
        CartState state = new();
        int raised = 0;
        state.OnChange += () => raised++;

        state.AddItem(_menu.GetById(1)!, DrinkSize.M, 1);
        state.Clear();

        Assert.Equal(2, raised);
    }

    // Kiểm tra: mã hợp lệ (không phân biệt hoa/thường, có khoảng trắng) tìm được; mã lạ trả về null.
    [Fact]
    public void DiscountService_KnownAndUnknownCodes()
    {
        DiscountService service = new();

        Assert.NotNull(service.FindByCode(" giam20k "));
        Assert.NotNull(service.FindByCode("MEMBER10"));
        Assert.Null(service.FindByCode("FREE"));
    }

    // Kiểm tra: OrderStore gán mã tăng dần CC-0001, CC-0002 và tìm lại theo mã không phân biệt hoa/thường.
    [Fact]
    public void OrderStore_AssignsSequentialIds()
    {
        OrderStore store = new();
        Cart cart = new();
        cart.AddItem(_menu.GetById(7)!);

        Order first = store.Add(cart.ToOrder(new Customer("An", "0901234567")));
        Order second = store.Add(cart.ToOrder(new Customer("Bình", "0912345678")));

        Assert.Equal("CC-0001", first.Id);
        Assert.Equal("CC-0002", second.Id);
        Assert.Same(second, store.GetById("cc-0002"));
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

    // Kiểm tra: CreatePayment trả về đúng class con Payment theo lựa chọn trên form.
    [Theory]
    [InlineData(PaymentMethod.Cash, typeof(CashPayment))]
    [InlineData(PaymentMethod.Card, typeof(CardPayment))]
    [InlineData(PaymentMethod.Momo, typeof(MomoPayment))]
    public void CheckoutModel_CreatesMatchingPayment(PaymentMethod method, Type expected)
    {
        CheckoutModel model = new() { FullName = "An", PhoneNumber = "0901234567", PaymentMethod = method, CardNumber = "411111111111" };

        Assert.IsType(expected, model.CreatePayment(50000));
    }
}
