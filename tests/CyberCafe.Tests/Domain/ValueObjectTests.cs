// ============================================================================
// ValueObjectTests.cs — value object Money, PhoneNumber, OrderCode (Buổi 50).
// 3 tính chất cần kiểm: (1) so sánh bằng GIÁ TRỊ, (2) BẤT BIẾN (phép toán trả object mới),
// (3) TỰ VALIDATE (đã tạo được là hợp lệ).
// 👉 Bước 1–4 (b50.md)
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.People;

namespace CyberCafe.Tests.Domain;

public class ValueObjectTests
{
    // Kiểm tra: 2 Money cùng số tiền là BẰNG nhau (khác class thường so tham chiếu).
    [Fact]
    public void Money_EqualsByValue()
    {
        Assert.Equal(new Money(29000), new Money(29000));
        Assert.True(new Money(29000) == new Money(29000m));
        Assert.NotEqual(new Money(29000), new Money(29001));
    }

    // Kiểm tra: không tạo được tiền âm, kể cả bằng phép trừ.
    [Fact]
    public void Money_NeverNegative()
    {
        Assert.Throws<ArgumentException>(() => new Money(-1));
        Assert.Throws<ArgumentException>(() => new Money(10000) - new Money(20000));
    }

    // Kiểm tra: phép toán trả Money MỚI, không đổi toán hạng (bất biến); Sum dãy rỗng = 0.
    [Fact]
    public void Money_Arithmetic_IsImmutable()
    {
        Money price = new(34000);

        Money line = price * 2;
        Money total = line + new Money(35000);

        Assert.Equal(new Money(34000), price);
        Assert.Equal(new Money(103000), total);
        Assert.Equal(Money.Zero, Money.Sum([]));
        Assert.True(total > line);
    }

    // Kiểm tra: ToString cho chuỗi thông báo; ToString("N0") cho Razor (giữ cú pháp như khi còn là decimal).
    [Fact]
    public void Money_Formatting()
    {
        Money money = new(83000);

        Assert.Equal(83000m.ToString("N0") + " đ", money.ToString());
        Assert.Equal(83000m.ToString("N0"), money.ToString("N0"));
    }

    // Kiểm tra: PhoneNumber chuẩn hóa khoảng trắng, so sánh theo giá trị, sai định dạng → ArgumentException.
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData(" 09012345678 ", true)]
    [InlineData("901234567", false)]
    [InlineData("09012a4567", false)]
    [InlineData("", false)]
    public void PhoneNumber_CreateValidates(string input, bool valid)
    {
        if (valid)
        {
            Assert.Equal(input.Trim(), PhoneNumber.Create(input).Value);
            Assert.Equal(PhoneNumber.Create(input), PhoneNumber.Create(input.Trim()));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => PhoneNumber.Create(input));
        }
    }

    // Kiểm tra: Person dùng CHUNG luật với PhoneNumber (1 nguồn sự thật).
    [Theory]
    [InlineData("0901234567")]
    [InlineData("090123456")]
    [InlineData("0901234567890")]
    public void Person_PhoneRule_DelegatesToValueObject(string phone)
    {
        Assert.Equal(PhoneNumber.IsValid(phone), Person.IsValidPhoneNumber(phone));
    }

    // Kiểm tra: OrderCode tạo "CC-0007" từ Id và đọc ngược được (không phân biệt hoa/thường, bỏ khoảng trắng).
    [Fact]
    public void OrderCode_FormatsAndParses()
    {
        Assert.Equal("CC-0007", OrderCode.From(7).Value);
        Assert.Equal("CC-12345", OrderCode.From(12345).ToString());
        Assert.True(OrderCode.TryParse(" cc-0007 ", out OrderCode parsed));
        Assert.Equal(OrderCode.From(7), parsed);
        Assert.False(OrderCode.TryParse("DH-0007", out _));
        Assert.False(OrderCode.TryParse("CC-abc", out _));
        Assert.Equal("CC-0042", Order.FormatCode(42)); // API cũ của Web vẫn dùng được
    }
}
