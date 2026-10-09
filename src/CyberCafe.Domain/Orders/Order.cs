// ============================================================================
// Order.cs — đơn hàng đã chốt (Buổi 24–31 · Composition + Polymorphism;
//            Buổi 32–41: Id do database sinh, trạng thái chỉ đổi qua ChangeStatus, constructor cho EF Core).
// EF Core lưu nguyên class domain này (không có class "entity" riêng) nhờ 3 thay đổi nhỏ:
//   1) Id kiểu int do SQL Server sinh (IDENTITY), mã hiển thị CC-0001 thành property tính toán Code.
//   2) Status có private set → muốn đổi phải qua ChangeStatus (kiểm tra luồng trạng thái hợp lệ).
//   3) Constructor rỗng private cho EF (xem cuối class).
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

    // Buổi 24–31: Id là chuỗi do OrderStore gán. Buổi 32–41: SQL Server tự tăng (IDENTITY) khi SaveChanges.
    /// <summary>Khóa chính, do database sinh khi lưu (0 = chưa lưu). <c>private set</c>: chỉ EF gán.</summary>
    public int Id { get; private set; }

    /// <summary>Mã đơn hiển thị cho khách, vd "CC-0007". Tính từ Id nên không cần lưu thành cột.</summary>
    public string Code => FormatCode(this.Id);

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

    /// <summary>
    /// Trạng thái xử lý đơn, mặc định Pending. <c>private set</c> (Buổi 32–41): đổi qua
    /// <see cref="ChangeStatus"/> để không ai nhảy cóc Pending → Completed.
    /// </summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

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

    // 👉 Bước 7 (b40.md): luật nghiệp vụ nằm trong domain, controller chỉ gọi.
    /// <summary>
    /// Chuyển trạng thái theo <see cref="OrderStatusFlow"/> (Pending → Preparing → Ready → Completed; hủy khi chưa xong).
    /// </summary>
    /// <exception cref="InvalidOperationException">Bước chuyển không hợp lệ (vd Pending → Ready).</exception>
    public void ChangeStatus(OrderStatus next)
    {
        if (!OrderStatusFlow.CanChange(this.Status, next))
        {
            throw new InvalidOperationException(
                $"Không thể chuyển đơn {this.Code} từ {OrderStatusFlow.Describe(this.Status)} sang {OrderStatusFlow.Describe(next)}");
        }

        this.Status = next;
    }

    /// <summary>"CC-" + Id đủ 4 chữ số. Static để Web/Api định dạng giống hệt nhau.</summary>
    public static string FormatCode(int id) => $"CC-{id:0000}";

    // 👉 Bước 4 (b40.md): constructor rỗng PRIVATE cho EF Core.
    // Constructor public nhận items/discount (navigation) → EF không gọi được; EF dùng constructor này
    // rồi gán từng cột vào backing field (_items, Customer, Note...) — KHÔNG chạy qua validate.
    // Dữ liệu trong DB đã hợp lệ từ lúc lưu nên bỏ qua validate là chấp nhận được.
    private Order()
    {
        this._items = [];
        this.Customer = null!; // EF gán từ các cột CustomerName/CustomerPhone (owned type)
    }
}
