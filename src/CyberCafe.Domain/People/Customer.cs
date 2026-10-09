// ============================================================================
// Customer.cs — khách hàng (Buổi 24–31 · Inheritance, constructor overloading; Buổi 50: overload nhận PhoneNumber).
// ============================================================================
using CyberCafe.Domain.Common;

namespace CyberCafe.Domain.People;

/// <summary>
/// Customer kế thừa Person, thêm LoyaltyPoints (điểm tích lũy).
/// </summary>
public class Customer : Person
{
    private int _loyaltyPoints;

    /// <summary>
    /// Điểm tích lũy. <c>private set</c>: bên ngoài không gán thẳng được,
    /// chỉ tăng qua <see cref="AddLoyaltyPoints"/> (Encapsulation).
    /// </summary>
    public int LoyaltyPoints
    {
        get => this._loyaltyPoints;
        private set
        {
            if (value < 0)
            {
                throw new ArgumentException("Điểm tích lũy không hợp lệ");
            }

            this._loyaltyPoints = value;
        }
    }

    /// <summary>Khách mới, 0 điểm.</summary>
    public Customer(string fullName, string phoneNumber) : base(fullName, phoneNumber)
    {
    }

    // Buổi 50: nhận value object → nơi gọi đã cầm SĐT "chắc chắn hợp lệ" (PlaceOrderCommandHandler dùng overload này)
    /// <summary>Khách mới, 0 điểm, SĐT đã là <see cref="PhoneNumber"/>.</summary>
    public Customer(string fullName, PhoneNumber phoneNumber) : base(fullName, phoneNumber.Value)
    {
    }

    /// <summary>Khách có sẵn điểm (constructor overloading: cùng tên, khác tham số).</summary>
    public Customer(string fullName, string phoneNumber, int loyaltyPoints) : base(fullName, phoneNumber)
    {
        this.LoyaltyPoints = loyaltyPoints;
    }

    /// <summary>Cộng điểm (phải &gt; 0). Order.Checkout gọi: mỗi 10.000 đ = 1 điểm.</summary>
    public void AddLoyaltyPoints(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentException("Số điểm cộng phải lớn hơn 0");
        }

        this.LoyaltyPoints += points;
    }

    /// <summary>Mô tả của Person + số điểm.</summary>
    public override string Display()
    {
        return $"{base.Display()} | Điểm: {this.LoyaltyPoints}";
    }
}
