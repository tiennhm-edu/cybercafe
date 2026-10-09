// ============================================================================
// OrderConfiguration.cs — map Order + Customer + OrderItem (Buổi 36–41 · 48 · 49–50 · Fluent API, owned type,
//                         quan hệ 1-n, value conversion cho enum, backing field, value object Money).
// Lược đồ sinh ra:
//   Orders(Id, Status, CreatedAt, Note, CustomerName, CustomerPhone, CustomerLoyaltyPoints, DiscountId, PaymentId, UserId)
//   OrderItems(Id, OrderId → Orders, ProductId → Products, Size, Quantity, UnitPrice)
// Buổi 49–50 (DDD): Order thành aggregate (DomainEvents, OwnerId), UnitPrice thành Money — nhưng LƯỢC ĐỒ
//   KHÔNG ĐỔI 1 cột nào → không cần migration mới (Migrations_AreUpToDate vẫn xanh). Đây là lợi ích của
//   Fluent API: đổi mô hình C# mà vẫn map vào đúng bảng cũ.
// ============================================================================
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Orders.</summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // 👉 Bước 5 (b40.md)
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id); // int → SQL Server IDENTITY(1,1)

        // 👉 Bước 4 (b49.md): domain event chỉ sống trong bộ nhớ tới khi phát — KHÔNG phải cột/bảng.
        // ⚠️ Lỗi hay gặp: quên Ignore → EF cố map IReadOnlyCollection<IDomainEvent> thành quan hệ → lỗi khi dựng model.
        builder.Ignore(o => o.DomainEvents);
        // Code (OrderCode) tính từ Id; các tổng tiền (Money) tính từ Items — không lưu
        builder.Ignore(o => o.Code);

        // Enum → chuỗi: cột Status lưu "Pending" thay vì số 0.
        // Đọc DB bằng mắt dễ hơn, và chèn thêm giá trị enum ở giữa không làm "lệch" dữ liệu cũ.
        // ⚠️ Lỗi hay gặp: đổi TÊN 1 giá trị enum sau khi đã có dữ liệu → dòng cũ không đọc lại được.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        // Note / CreatedAt có private set trong domain → EF vẫn ghi được khi đọc từ DB.
        builder.Property(o => o.Note).HasMaxLength(200);
        builder.Property(o => o.CreatedAt);

        // Index cho các truy vấn hay dùng: màn hình barista lọc theo Status, báo cáo lọc theo ngày.
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);

        // Owned type: Customer không có bảng/khóa riêng — các cột nằm LUÔN trong bảng Orders.
        // Hợp lý vì đây là "ảnh chụp" thông tin khách lúc đặt đơn (buổi 42–47 mới có bảng Users).
        builder.OwnsOne(o => o.Customer, customer =>
        {
            customer.Property(c => c.FullName).HasColumnName("CustomerName").HasMaxLength(50).IsRequired();
            customer.Property(c => c.PhoneNumber).HasColumnName("CustomerPhone").HasMaxLength(11).IsRequired();
            customer.Property(c => c.LoyaltyPoints).HasColumnName("CustomerLoyaltyPoints");
        });
        builder.Navigation(o => o.Customer).IsRequired();

        // 1 Order – n OrderItem. Khóa ngoại OrderId là SHADOW PROPERTY: có trong bảng, không có trong class
        // → domain không phải biết khái niệm "khóa ngoại". Xóa đơn → xóa luôn các dòng (Cascade).
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .IsRequired()                       // dòng đơn không thể "mồ côi" → cột OrderId NOT NULL
            .OnDelete(DeleteBehavior.Cascade);

        // Items là IReadOnlyList bọc field _items: bảo EF đọc/ghi thẳng vào field (không có setter để gọi).
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Discount / Payment là cây kế thừa riêng (xem PaymentConfiguration.cs) → bảng riêng, quan hệ 1-1.
        // Khóa ngoại DiscountId / PaymentId cũng là shadow property, đặt ở phía Orders.
        builder.HasOne(o => o.Discount).WithOne().HasForeignKey<Order>("DiscountId");
        builder.HasOne(o => o.Payment).WithOne().HasForeignKey<Order>("PaymentId");

        // Buổi 42–48: chủ đơn là SHADOW property "UserId" (domain không biết User).
        // 👉 Bước 4 (b49.md): giờ là property THẬT Order.OwnerId — map vào ĐÚNG cột cũ "UserId" (HasColumnName)
        // → tên cột, khóa ngoại FK_Orders_Users_UserId, index IX_Orders_UserId giữ nguyên, DB cũ không phải sửa.
        // Nullable: đơn tạo trước khi có đăng nhập (b40) vẫn hợp lệ. Restrict: không xóa user đang có đơn.
        builder.Property(o => o.OwnerId).HasColumnName(OwnerUserIdColumn);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Tên cột khóa ngoại tới Users (giữ từ b42 để SQL viết tay / báo cáo dùng lại).</summary>
    public const string OwnerUserIdColumn = "UserId";
}

/// <summary>Cấu hình bảng OrderItems.</summary>
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        // Domain OrderItem không có Id → khai báo khóa SHADOW "Id" (IDENTITY) chỉ tồn tại trong DB.
        builder.Property<int>("Id");
        builder.HasKey("Id");

        builder.Property(i => i.Size).HasConversion<string>().HasMaxLength(1);
        builder.Property(i => i.Quantity);

        // 👉 Bước 2 (b50.md): VALUE CONVERTER — C# dùng Money (value object), cột vẫn là decimal(18,2).
        //   Ghi: Money → m.Amount;  Đọc: decimal → new Money(v) (đi qua constructor → vẫn kiểm tra không âm).
        // Cách khác: OwnsOne / ComplexProperty (EF 8+) khi value object có NHIỀU cột (vd Money + Currency).
        builder.Property(i => i.UnitPrice)
            .HasConversion(money => money.Amount, amount => new Money(amount))
            .HasPrecision(18, 2);

        // n OrderItem – 1 Product. Restrict: KHÔNG cho xóa món đã có trong đơn (mất lịch sử bán hàng).
        // ⚠️ EF InMemory (dùng trong test) không kiểm tra khóa ngoại → MenuService tự kiểm tra (IsSoldAsync) trước khi xóa.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Restrict);

        // TotalPrice là property tính toán (UnitPrice × Quantity) → không thành cột.
    }
}
