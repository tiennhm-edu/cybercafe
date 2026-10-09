// ============================================================================
// PaymentConfiguration.cs — thêm 2 cây kế thừa TPH: Payment và Discount (Buổi 36–41).
// Cùng kỹ thuật như Products: 1 bảng + cột discriminator. Khác Products ở chỗ domain
// Payment/Discount KHÔNG có property Id → dùng khóa shadow "Id".
// Constructor của CashPayment(amount, cashReceived)... có tên tham số trùng tên property
// → EF gọi THẲNG constructor đó khi đọc dữ liệu (constructor binding), không cần constructor rỗng.
// ============================================================================
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CyberCafe.Api.Data.Configurations;

/// <summary>Bảng Payments: Cash / Card / Momo.</summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.Property<int>("Id");
        builder.HasKey("Id");

        // Payment là abstract → chỉ 3 class con cần giá trị discriminator.
        builder.HasDiscriminator<string>("PaymentType")
            .HasValue<CashPayment>("cash")
            .HasValue<CardPayment>("card")
            .HasValue<MomoPayment>("momo");
        builder.Property<string>("PaymentType").HasMaxLength(10);

        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.IsPaid);
    }
}

/// <summary>Cột riêng của CashPayment.</summary>
public class CashPaymentConfiguration : IEntityTypeConfiguration<CashPayment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CashPayment> builder) =>
        builder.Property(p => p.CashReceived).HasPrecision(18, 2);
}

/// <summary>Cột riêng của CardPayment. CardNumber chỉ chứa "****1234" (PaymentFactory đã che).</summary>
public class CardPaymentConfiguration : IEntityTypeConfiguration<CardPayment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CardPayment> builder)
    {
        builder.Property(p => p.CardNumber).HasMaxLength(20);
        builder.Property(p => p.BankName).HasMaxLength(50);
    }
}

/// <summary>Cột riêng của MomoPayment.</summary>
public class MomoPaymentConfiguration : IEntityTypeConfiguration<MomoPayment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MomoPayment> builder) =>
        builder.Property(p => p.PhoneNumber).HasColumnName("MomoPhone").HasMaxLength(11);
}

/// <summary>
/// Bảng OrderDiscounts: giảm giá ĐÃ ÁP DỤNG cho từng đơn (mỗi đơn 1 dòng riêng).
/// Lưu cả tham số (Percent / Amount) → đổi chương trình khuyến mãi sau này không làm sai đơn cũ.
/// </summary>
public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("OrderDiscounts");
        builder.Property<int>("Id");
        builder.HasKey("Id");

        builder.HasDiscriminator<string>("DiscountType")
            .HasValue<Discount>("basic")
            .HasValue<MemberDiscount>("member")
            .HasValue<VoucherDiscount>("voucher");
        builder.Property<string>("DiscountType").HasMaxLength(10);

        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
    }
}

/// <summary>Cột riêng của MemberDiscount.</summary>
public class MemberDiscountConfiguration : IEntityTypeConfiguration<MemberDiscount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemberDiscount> builder) =>
        builder.Property(d => d.Percent).HasPrecision(5, 2);
}

/// <summary>Cột riêng của VoucherDiscount.</summary>
public class VoucherDiscountConfiguration : IEntityTypeConfiguration<VoucherDiscount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VoucherDiscount> builder)
    {
        builder.Property(d => d.Code).HasMaxLength(20);
        builder.Property(d => d.Amount).HasPrecision(18, 2);
    }
}
