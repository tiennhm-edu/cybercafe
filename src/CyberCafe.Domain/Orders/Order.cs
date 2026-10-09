// ============================================================================
// Order.cs — đơn hàng (Buổi 24–31 · Composition + Polymorphism;
//            Buổi 32–41: Id do database sinh, trạng thái chỉ đổi qua ChangeStatus, constructor cho EF Core;
//            Buổi 49–50: AGGREGATE ROOT — mọi thay đổi đi qua method có tên nghiệp vụ, tự bảo vệ invariant,
//                         phát domain event; tiền là value object Money, mã đơn là OrderCode).
// Vòng đời 1 đơn (ai gọi gì):
//   Order.Create(...)  → AddItem(...) × n → ApplyDiscount(...)? → Pay(payment)        [khách đặt]
//   StartPreparing() → MarkReady() → Complete()                                       [barista]
//   Cancel(...)  khi Pending (khách) hoặc Pending/Preparing (nhân viên)
// INVARIANT (luật luôn đúng, aggregate tự giữ — không trông vào controller/handler):
//   1) Đã thanh toán thì KHÔNG thêm món / đổi giảm giá được nữa.
//   2) Không thanh toán đơn rỗng; không thanh toán 2 lần.
//   3) Chưa thanh toán thì không pha chế được.
//   4) Trạng thái chỉ đi theo OrderStatusFlow; khách chỉ tự hủy khi Pending.
//   5) Không thêm món đang tạm hết.
// So với b48: không còn public constructor nhận sẵn danh sách món, không còn Checkout() ai gọi mấy lần cũng được.
// Buổi 55 (microservice): thanh toán có thể đến SAU, bất đồng bộ từ Payment service:
//   Order.Create → AddItem → (lưu, chờ)  …  PaymentCompleted → Pay(payment)  |  PaymentFailed → RejectPayment(lý do)
//   Trong lúc chờ: Pending + chưa trả tiền = IsAwaitingPayment. Invariant 3 ("chưa trả chưa pha") giữ nguyên.
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;

namespace CyberCafe.Domain.Orders;

/// <summary>
/// Aggregate root "đơn hàng": chứa OrderItem, Discount, Payment, thông tin khách.
/// Bên ngoài chỉ ĐỌC được dữ liệu (không setter public) và GỌI method nghiệp vụ.
/// </summary>
public class Order : AggregateRoot
{
    // Field (không phải List public): bên ngoài nhận IReadOnlyList → không .Add() lén được
    private readonly List<OrderItem> _items = [];

    // Buổi 24–31: Id là chuỗi do OrderStore gán. Buổi 32–41: SQL Server tự tăng (IDENTITY) khi SaveChanges.
    /// <summary>Khóa chính, do database sinh khi lưu (0 = chưa lưu). <c>private set</c>: chỉ EF gán.</summary>
    public int Id { get; private set; }

    /// <summary>Mã đơn hiển thị cho khách, vd "CC-0007" — value object tính từ Id (không lưu cột riêng).</summary>
    public OrderCode Code => OrderCode.From(this.Id);

    // 👉 Bước 4 (b49.md): b42–48 đây là SHADOW PROPERTY "UserId" mà Domain không biết. DDD: "đơn của ai" là
    // thông tin nghiệp vụ → đưa vào aggregate. Tham chiếu aggregate KHÁC (tài khoản) bằng ID, không bằng object User.
    /// <summary>Id tài khoản đã đặt đơn (null = đơn cũ trước khi có đăng nhập). Cột Orders.UserId.</summary>
    public int? OwnerId { get; private set; }

    /// <summary>Khách đặt đơn (ảnh chụp tên/SĐT/điểm lúc đặt).</summary>
    public Customer Customer { get; private set; }

    /// <summary>Các dòng của đơn (chỉ đọc). Muốn thêm phải qua <see cref="AddItem"/>.</summary>
    public IReadOnlyList<OrderItem> Items => this._items;

    /// <summary>Giảm giá đã áp dụng (có thể null). Đổi qua <see cref="ApplyDiscount"/>.</summary>
    public Discount? Discount { get; private set; }

    /// <summary>Thông tin thanh toán; null cho tới khi gọi <see cref="Pay"/>.</summary>
    public Payment? Payment { get; private set; }

    /// <summary>Ghi chú của khách (đã Trim; chuỗi rỗng được lưu thành null).</summary>
    public string? Note { get; private set; }

    /// <summary>Thời điểm tạo đơn.</summary>
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    /// <summary>Trạng thái xử lý, mặc định Pending. Chỉ đổi qua StartPreparing/MarkReady/Complete/Cancel.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>Đã thanh toán xong chưa.</summary>
    public bool IsPaid => this.Payment is { IsPaid: true };

    /// <summary>
    /// Buổi 55: đơn đã đặt nhưng Payment service chưa trả lời (Pending, chưa trả tiền).
    /// Màn hình quầy KHÔNG hiện các đơn này; trang của khách hiện "Đang xử lý thanh toán".
    /// </summary>
    public bool IsAwaitingPayment => this.Status == OrderStatus.Pending && !this.IsPaid;

    /// <summary>Tổng tiền trước giảm giá.</summary>
    public Money TotalAmount => Money.Sum(this._items.Select(x => x.TotalPrice));

    /// <summary>Số tiền được giảm (Discount của buổi OOP vẫn tính trên decimal → bọc lại thành Money).</summary>
    public Money DiscountAmount => this.Discount is null ? Money.Zero : new Money(this.Discount.GetDiscountAmount(this.TotalAmount.Amount));

    /// <summary>Số tiền khách phải trả.</summary>
    public Money FinalAmount => this.TotalAmount - this.DiscountAmount;

    /// <summary>Tổng số món.</summary>
    public int ItemCount => this._items.Sum(x => x.Quantity);

    // 👉 Bước 2 (b49.md): FACTORY METHOD thay cho public constructor — tên nói rõ ý định, và là chỗ duy nhất
    // tạo đơn hợp lệ ban đầu (Pending, chưa có món, chưa thanh toán).
    /// <summary>Tạo đơn mới (chưa có món).</summary>
    /// <param name="customer">Khách (đã validate họ tên + SĐT).</param>
    /// <param name="note">Ghi chú (tùy chọn).</param>
    /// <param name="ownerId">Id tài khoản đặt đơn — lấy từ TOKEN, không từ body.</param>
    public static Order Create(Customer customer, string? note = null, int? ownerId = null) => new()
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer)),
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        OwnerId = ownerId,
    };

    /// <summary>
    /// Thêm món. Cùng món + cùng size thì cộng dồn số lượng (giống Cart); món không có size luôn là S.
    /// </summary>
    /// <exception cref="DomainException">Đơn đã thanh toán / đã xử lý, hoặc món đang tạm hết (→ 409).</exception>
    public OrderItem AddItem(Product product, DrinkSize size = DrinkSize.S, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(product);
        this.EnsureEditable();

        if (!product.IsAvailable)
        {
            throw new DomainException($"{product.Name} đang tạm hết");
        }

        DrinkSize effectiveSize = product.HasSize ? size : DrinkSize.S;
        OrderItem? existing = this._items.FirstOrDefault(x => x.Product.Id == product.Id && x.Size == effectiveSize);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity); // OrderItem.Quantity là internal set → chỉ Domain sửa được
            return existing;
        }

        OrderItem item = new(product, effectiveSize, quantity); // chốt đơn giá lúc thêm
        this._items.Add(item);
        return item;
    }

    /// <summary>Áp (hoặc thay) giảm giá — chỉ khi chưa thanh toán.</summary>
    /// <exception cref="DomainException">Đơn đã thanh toán / đã xử lý.</exception>
    public void ApplyDiscount(Discount discount)
    {
        ArgumentNullException.ThrowIfNull(discount);
        this.EnsureEditable();
        this.Discount = discount;
    }

    // 👉 Bước 3 (b49.md) · 👉 Bước 5 (b50.md)
    /// <summary>
    /// Thanh toán bằng <paramref name="payment"/> (Cash/Card/Momo — đa hình như buổi OOP), cộng điểm
    /// (10.000 đ = 1 điểm), rồi phát <see cref="OrderPaid"/> + <see cref="OrderPlaced"/>.
    /// </summary>
    /// <returns>Thông điệp kết quả do từng loại Payment tự tạo.</returns>
    /// <exception cref="DomainException">Đơn rỗng, đã thanh toán, hoặc không còn Pending.</exception>
    public string Pay(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (this._items.Count == 0)
        {
            throw new DomainException("Đơn hàng không có món nào");
        }

        this.EnsureEditable(); // đã trả tiền / đã hủy → không trả lần nữa

        payment.Amount = this.FinalAmount.Amount;
        string result = payment.Process(); // Polymorphism: mỗi class con tự biết cách Process()
        this.Payment = payment;

        int points = (int)(this.FinalAmount.Amount / 10000);
        if (points > 0)
        {
            this.Customer.AddLoyaltyPoints(points);
        }

        // Chỉ GHI LẠI sự kiện; ai nghe (SignalR, log...) là chuyện của tầng ngoài, phát SAU khi lưu DB.
        DateTime now = DateTime.Now;
        this.Raise(new OrderPaid(this, this.FinalAmount, now));
        this.Raise(new OrderPlaced(this, now));
        return result;
    }

    /// <summary>Barista bắt đầu pha (Pending → Preparing). Đơn phải đã thanh toán.</summary>
    public void StartPreparing()
    {
        if (!this.IsPaid)
        {
            throw new DomainException($"Đơn {this.Code} chưa thanh toán, chưa pha chế được");
        }

        this.TransitionTo(OrderStatus.Preparing);
    }

    /// <summary>Pha xong, chờ khách nhận (Preparing → Ready).</summary>
    public void MarkReady() => this.TransitionTo(OrderStatus.Ready);

    /// <summary>Khách đã nhận (Ready → Completed).</summary>
    public void Complete() => this.TransitionTo(OrderStatus.Completed);

    /// <summary>
    /// Hủy đơn. Khách tự hủy chỉ khi barista chưa bắt đầu (Pending); nhân viên hủy theo OrderStatusFlow
    /// (Pending/Preparing). b48 luật "khách chỉ hủy khi Pending" nằm ở OrderService — giờ là luật của aggregate.
    /// </summary>
    /// <param name="requestedByCustomer">true = khách bấm hủy; false = nhân viên quầy.</param>
    public void Cancel(bool requestedByCustomer = false)
    {
        if (requestedByCustomer && this.Status != OrderStatus.Pending)
        {
            throw new DomainException("Quán đã bắt đầu pha chế, vui lòng liên hệ quầy để hủy.");
        }

        this.TransitionTo(OrderStatus.Cancelled);
    }

    // 👉 Bước 4 (b55.md): Payment service từ chối → hủy đơn. Đi qua TransitionTo như mọi lần đổi trạng thái
    // → phát OrderStatusChanged → handler cũ (b50) báo SignalR cho khách. Không cần handler mới.
    /// <summary>Thanh toán bị từ chối (vượt hạn mức, thẻ bị từ chối...) → hủy đơn đang chờ thanh toán.</summary>
    /// <param name="reason">Lý do từ Payment service (để log; chưa lưu cột riêng).</param>
    /// <exception cref="DomainException">Đơn đã thanh toán rồi (không hủy vì 1 message lạc) hoặc đã đi khỏi Pending.</exception>
    public void RejectPayment(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (this.IsPaid)
        {
            // ⚠️ Lỗi hay gặp: tin mọi message tới → đơn đã trả tiền bị hủy vì 1 message PaymentFailed cũ/đến trễ.
            throw new DomainException($"Đơn {this.Code} đã thanh toán, không thể đánh dấu thanh toán thất bại");
        }

        this.TransitionTo(OrderStatus.Cancelled); // chỉ hợp lệ từ Pending/Preparing; chưa trả tiền thì chắc chắn Pending
    }

    // 👉 Bước 7 (b40.md) · 👉 Bước 3 (b49.md): nút barista / PUT /status gửi "trạng thái đích" →
    // dịch sang ĐÚNG method nghiệp vụ (để luật riêng của từng bước như "phải trả tiền rồi mới pha" luôn chạy).
    /// <summary>Chuyển sang trạng thái <paramref name="next"/> (dùng cho màn hình quầy).</summary>
    /// <exception cref="DomainException">Bước chuyển không hợp lệ (vd Pending → Ready) (→ 409).</exception>
    public void ChangeStatus(OrderStatus next)
    {
        switch (next)
        {
            case OrderStatus.Preparing: this.StartPreparing(); break;
            case OrderStatus.Ready: this.MarkReady(); break;
            case OrderStatus.Completed: this.Complete(); break;
            case OrderStatus.Cancelled: this.Cancel(); break;
            default: throw this.InvalidTransition(next); // "quay lại Pending" không có method nào → luôn sai
        }
    }

    /// <summary>Đơn có thuộc tài khoản <paramref name="userId"/> không (đơn không chủ thì không thuộc ai).</summary>
    public bool IsOwnedBy(int? userId) => this.OwnerId is not null && this.OwnerId == userId;

    /// <summary>"CC-" + Id đủ 4 chữ số. Static để Web định dạng giống hệt Api (giữ từ b40, giờ đi qua OrderCode).</summary>
    public static string FormatCode(int id) => OrderCode.From(id).Value;

    // Đổi trạng thái theo bảng luật OrderStatusFlow (Web dùng chung bảng này để vẽ nút) + phát event
    private void TransitionTo(OrderStatus next)
    {
        if (!OrderStatusFlow.CanChange(this.Status, next))
        {
            throw this.InvalidTransition(next);
        }

        OrderStatus from = this.Status;
        this.Status = next;
        this.Raise(new OrderStatusChanged(this, from, next, DateTime.Now));
    }

    private DomainException InvalidTransition(OrderStatus next) => new(
        $"Không thể chuyển đơn {this.Code} từ {OrderStatusFlow.Describe(this.Status)} sang {OrderStatusFlow.Describe(next)}");

    // Invariant 1: chỉ sửa món/giảm giá/thanh toán khi đơn còn "nháp" (Pending và chưa trả tiền)
    private void EnsureEditable()
    {
        if (this.IsPaid)
        {
            throw new DomainException($"Đơn {this.Code} đã thanh toán, không sửa được nữa");
        }

        if (this.Status != OrderStatus.Pending)
        {
            throw new DomainException($"Đơn {this.Code} đang {OrderStatusFlow.Describe(this.Status)}, không sửa được nữa");
        }
    }

    // 👉 Bước 4 (b40.md): constructor rỗng PRIVATE — dùng cho EF Core (đọc từ DB) và cho Create(...) ở trên.
    // EF gán từng cột vào property/backing field — KHÔNG chạy qua validate (dữ liệu trong DB đã hợp lệ lúc lưu).
    private Order()
    {
        this.Customer = null!; // Create(...) gán ngay; EF gán từ các cột CustomerName/CustomerPhone (owned type)
    }
}
