// ============================================================================
// Drink.cs — lớp trung gian abstract cho đồ uống (Buổi 24–31 · Abstraction).
// Gom logic phụ thu size vào 1 chỗ: Coffee và Tea kế thừa, không phải viết lại.
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Lớp trung gian cho đồ uống có size (Coffee, Tea).
/// Phụ thu: M +5.000đ, L +10.000đ.
/// </summary>
public abstract class Drink : Product
{
    /// <summary>Phụ thu size M (VND). <c>const</c>: hằng số biên dịch, dùng được như Drink.SizeMSurcharge.</summary>
    public const decimal SizeMSurcharge = 5000;

    /// <summary>Phụ thu size L (VND).</summary>
    public const decimal SizeLSurcharge = 10000;

    // abstract class không new được (new Drink(...) → lỗi biên dịch); constructor protected chỉ cho class con gọi
    /// <summary>Chỉ gọi được từ class con (Coffee, Tea) qua <c>: base(name, price)</c>.</summary>
    protected Drink(string name, decimal price) : base(name, price)
    {
    }

    /// <summary>Đồ uống luôn có size.</summary>
    public override bool HasSize => true;

    /// <summary>Giá cơ bản + phụ thu theo size (switch expression).</summary>
    public override decimal GetPrice(DrinkSize size)
    {
        return size switch
        {
            DrinkSize.M => this.Price + SizeMSurcharge,
            DrinkSize.L => this.Price + SizeLSurcharge,
            // _ = "mọi trường hợp còn lại" (ở đây là S)
            _ => this.Price
        };
    }
}
