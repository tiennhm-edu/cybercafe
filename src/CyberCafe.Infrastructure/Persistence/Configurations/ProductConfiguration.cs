// ============================================================================
// ProductConfiguration.cs — map cây kế thừa Product vào 1 bảng (Buổi 36–41 · 48 · TPH, Fluent API, HasData).
// TPH = Table-Per-Hierarchy: Product, Coffee, Tea, Cake chung bảng Products,
// cột "ProductType" (discriminator) cho biết dòng nào là loại nào:
//   Id | ProductType | Name          | Price | BeanType | TeaType | Flavor
//   1  | coffee      | Cà phê sữa đá | 29000 | Robusta  | NULL    | NULL
//   7  | cake        | Bánh tiramisu | 35000 | NULL     | NULL    | Cà phê
// So sánh nhanh (thảo luận trong buổi):
//   - TPH: 1 bảng, query nhanh nhất (không JOIN), đổi lại nhiều cột NULL. Mặc định của EF.
//   - TPT: mỗi class 1 bảng, JOIN khi đọc → chậm hơn khi cây sâu.
//   - TPC: mỗi class cụ thể 1 bảng đầy đủ cột, không JOIN nhưng UNION khi đọc kiểu cha.
// ============================================================================
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình bảng Products (gốc của cây kế thừa).</summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>Tên cột discriminator — dùng lại trong truy vấn SQL thô / báo cáo.</summary>
    public const string Discriminator = "ProductType";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // 👉 Bước 4 (b40.md)
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        // Discriminator: giá trị trùng ProductTypes (coffee/tea/cake) mà JSON dùng → dễ đối chiếu.
        // Product không abstract (giữ như bài OOP) nên cũng cần 1 giá trị; Drink abstract thì không cần.
        builder.HasDiscriminator<string>(Discriminator)
            .HasValue<Product>("product")
            .HasValue<Coffee>(ProductTypes.Coffee)
            .HasValue<Tea>(ProductTypes.Tea)
            .HasValue<Cake>(ProductTypes.Cake);
        builder.Property<string>(Discriminator).HasMaxLength(20);

        // Không cấu hình → nvarchar(max). Đặt độ dài để SQL Server đánh index được và chặn dữ liệu rác.
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(300);
        builder.Property(p => p.Emoji).HasMaxLength(16);

        // ⚠️ Lỗi hay gặp: decimal không khai báo precision → EF cảnh báo và SQL Server dùng decimal(18,2)
        // một cách "ngầm định". Luôn ghi rõ: tiền VND không có số lẻ nhưng giữ 2 chữ số cho an toàn khi tính %.
        builder.Property(p => p.Price).HasPrecision(18, 2);

        // Validate trong setter (Name, Price) chạy khi CODE của ta gán. Khi đọc từ DB, EF ghi thẳng vào
        // backing field (_name, _price) — mặc định PropertyAccessMode.PreferField — nên không chạy validate lại.
        builder.HasIndex(p => p.Name);

        // Category / HasSize là property chỉ có get (tính từ class) → EF tự bỏ qua, không thành cột.
    }
}

/// <summary>Cột riêng của Coffee (NULL với các loại khác — đặc trưng của TPH).</summary>
public class CoffeeConfiguration : IEntityTypeConfiguration<Coffee>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Coffee> builder)
    {
        builder.Property(c => c.BeanType).HasMaxLength(50);

        // HasData trên đúng kiểu con: EF biết dòng seed này có ProductType = 'coffee'.
        builder.HasData(MenuSeed.Products().OfType<Coffee>());
    }
}

/// <summary>Cột riêng của Tea.</summary>
public class TeaConfiguration : IEntityTypeConfiguration<Tea>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tea> builder)
    {
        builder.Property(t => t.TeaType).HasMaxLength(50);
        builder.HasData(MenuSeed.Products().OfType<Tea>());
    }
}

/// <summary>Cột riêng của Cake.</summary>
public class CakeConfiguration : IEntityTypeConfiguration<Cake>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Cake> builder)
    {
        builder.Property(c => c.Flavor).HasMaxLength(50);
        builder.HasData(MenuSeed.Products().OfType<Cake>());
    }
}
