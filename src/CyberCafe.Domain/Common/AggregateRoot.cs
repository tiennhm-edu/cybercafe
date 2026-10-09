// ============================================================================
// AggregateRoot.cs — lớp cha của mọi AGGREGATE ROOT (Buổi 49 · DDD building blocks; Buổi 50 · domain event).
// Aggregate = 1 cụm object thay đổi CÙNG NHAU và phải luôn hợp lệ CÙNG NHAU (Order + OrderItem + Discount + Payment).
// Aggregate root = cửa DUY NHẤT để đổi cụm đó: bên ngoài chỉ gọi method của Order, không sửa thẳng OrderItem.
// Lớp cha này chỉ thêm 1 thứ: danh sách domain event "đã xảy ra" chờ phát sau khi lưu DB thành công.
// Domain vẫn C# thuần: không biết EF Core, không biết ai sẽ nghe event (SignalR? email?) — chỉ GHI LẠI sự kiện.
// ============================================================================
namespace CyberCafe.Domain.Common;

// 👉 Bước 1 (b49.md) · 👉 Bước 5 (b50.md)
/// <summary>Lớp cha của aggregate root: giữ các domain event chưa được phát.</summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Sự kiện đã xảy ra trên aggregate, chờ hạ tầng (DbContext) phát SAU khi SaveChanges thành công.
    /// ⚠️ EF Core không được map property này thành cột/bảng → OrderConfiguration gọi Ignore(o => o.DomainEvents).
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => this._domainEvents;

    /// <summary>Xóa danh sách sau khi đã lấy ra để phát (tránh phát 2 lần ở lần SaveChanges sau).</summary>
    public void ClearDomainEvents() => this._domainEvents.Clear();

    /// <summary>Ghi nhận 1 sự kiện — chỉ class con (aggregate) gọi được, từ bên trong method nghiệp vụ.</summary>
    protected void Raise(IDomainEvent domainEvent) => this._domainEvents.Add(domainEvent);
}
