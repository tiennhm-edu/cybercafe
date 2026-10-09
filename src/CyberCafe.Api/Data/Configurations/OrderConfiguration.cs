// ============================================================================
// OrderConfiguration.cs — map Order + Customer + OrderItem (Buổi 36–41 · Fluent API, owned type,
//                         quan hệ 1-n, value conversion cho enum, backing field).
// Lược đồ sinh ra:
//   Orders(Id, Status, CreatedAt, Note, CustomerName, CustomerPhone, CustomerLoyaltyPoints, DiscountId, PaymentId)
//   OrderItems(Id, OrderId → Orders, ProductId → Products, Size, Quantity, UnitPrice)
// ============================================================================
using CyberCafe.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Api.Data.Configurations;

/// <summary>Cấu hình bảng Orders.</summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // 👉 Bước 5 (b40.md)
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id); // int → SQL Server IDENTITY(1,1)

        // Enum → chuỗi: cột Status lưu "Pending" thay vì số 0.
        // Đọc DB bằng mắt dễ hơn, và chèn thêm giá trị enum ở giữa không làm "lệch" dữ liệu cũ.
        // ⚠️ Lỗi hay gặp: đổi TÊN 1 giá trị enum sau khi đã có dữ liệu → dòng cũ không đọc lại được.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        // Note / CreatedAt chỉ có { get; } trong domain. Khai báo tường minh → EF dùng backing field
        // do compiler sinh để ghi giá trị khi đọc từ DB (property không có setter vẫn map được).
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
    }
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
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);

        // n OrderItem – 1 Product. Restrict: KHÔNG cho xóa món đã có trong đơn (mất lịch sử bán hàng).
        // ⚠️ EF InMemory (dùng trong test) không kiểm tra khóa ngoại → controller tự kiểm tra trước khi xóa.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Restrict);

        // TotalPrice là property tính toán (UnitPrice × Quantity) → không thành cột.
    }
}
