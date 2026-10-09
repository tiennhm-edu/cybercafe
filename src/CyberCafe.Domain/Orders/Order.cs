// ============================================================================
// Order.cs — đơn hàng đã chốt (Buổi 24–31 · Composition + Polymorphism).
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;

namespace CyberCafe.Domain.Orders;

/// <summary>
/// Đơn hàng đã chốt từ giỏ — port/đơn giản hóa từ day-04.
/// Composition: Order chứa nhiều OrderItem (bản sao từ Cart).
/// Discount + Payment là đa hình: Order không if theo loại.
/// </summary>
public class Order
{
    private readonly List<OrderItem> _items;

    // Id gán bởi OrderStore (vd: CC-0001). Buổi 41 sẽ do database sinh.
    /// <summary>Mã đơn, do OrderStore gán khi lưu.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Khách đặt đơn. Property chỉ có <c>get</c>: chỉ gán được trong constructor.</summary>
    public Customer Customer { get; }

    /// <summary>Các dòng của đơn (chỉ đọc).</summary>
    public IReadOnlyList<OrderItem> Items => this._items;

    /// <summary>Giảm giá đã áp dụng lúc chốt đơn (có thể null).</summary>
    public Discount? Discount { get; }

    /// <summary>Thông tin thanh toán; null cho tới khi gọi <see cref="Checkout"/>.</summary>
    public Payment? Payment { get; private set; }

    /// <summary>Ghi chú của khách (đã Trim; chuỗi rỗng được lưu thành null).</summary>
    public string? Note { get; }

    /// <summary>Thời điểm tạo đơn.</summary>
    public DateTime CreatedAt { get; } = DateTime.Now;

    /// <summary>Trạng thái xử lý đơn, mặc định Pending.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>Tổng tiền trước giảm giá.</summary>
    public decimal TotalAmount => this._items.Sum(x => x.TotalPrice);

    /// <summary>Số tiền được giảm.</summary>
    public decimal DiscountAmount => this.Discount?.GetDiscountAmount(this.TotalAmount) ?? 0;

    /// <summary>Số tiền khách phải trả.</summary>
    public decimal FinalAmount => this.TotalAmount - this.DiscountAmount;

    /// <summary>Tổng số món.</summary>
    public int ItemCount => this._items.Sum(x => x.Quantity);

    /// <summary>Tạo đơn từ danh sách dòng (thường gọi qua <c>Cart.ToOrder</c>).</summary>
    /// <exception cref="InvalidOperationException">Không có dòng nào.</exception>
    public Order(Customer customer, IEnumerable<OrderItem> items, Discount? discount = null, string? note = null)
    {
        this.Customer = customer ?? throw new ArgumentNullException(nameof(customer));

        // Copy từng dòng: sửa giỏ sau khi đặt KHÔNG làm đổi đơn
        // (OrderItem là class = kiểu tham chiếu; nếu giữ chung object, đổi số lượng trong giỏ sẽ đổi luôn đơn đã đặt)
        this._items = items.Select(x => new OrderItem(x.Product, x.Size, x.Quantity)).ToList();

        if (this._items.Count == 0)
        {
            throw new InvalidOperationException("Đơn hàng không có món nào");
        }

        this.Discount = discount;
        this.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    // Thanh toán: đồng bộ số tiền, gọi Process() đa hình, cộng điểm (10.000đ = 1 điểm)
    /// <summary>
    /// Thanh toán đơn bằng <paramref name="payment"/> (Cash/Card/Momo đều được — kiểu cha Payment).
    /// Trả về thông điệp kết quả do từng loại Payment tự tạo.
    /// </summary>
    public string Checkout(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        payment.Amount = this.FinalAmount;
        // Polymorphism: không cần if (payment is CashPayment)...; mỗi class con tự biết cách Process()
        string result = payment.Process();
        this.Payment = payment;

        int points = (int)(this.FinalAmount / 10000);
        if (points > 0)
        {
            this.Customer.AddLoyaltyPoints(points);
        }

        return result;
    }
}
