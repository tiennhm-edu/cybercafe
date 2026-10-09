// ============================================================================
// ProductMapper.cs — ProductDto (JSON) → domain Coffee/Tea/Cake (Buổi 32–35).
// Vì sao không dùng thẳng ProductDto trên trang Menu?
//   ProductCard, CartState, Cart (buổi 24–31) làm việc với domain Product: GetPrice(size) đa hình,
//   HasSize, Category... Đổi ngược DTO → domain ở 1 chỗ này thì TOÀN BỘ component cũ giữ nguyên.
// ============================================================================
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Products;

namespace CyberCafe.Web.Services;

/// <summary>Dựng lại object domain từ dữ liệu API.</summary>
public static class ProductMapper
{
    /// <summary>"coffee" → new Coffee(...), "tea" → new Tea(...), còn lại → new Cake(...).</summary>
    public static Product ToDomain(this ProductDto dto)
    {
        // switch expression trả về kiểu CHA Product — giống MenuService buổi 24–31, chỉ khác nguồn dữ liệu
        Product product = dto.Type switch
        {
            ProductTypes.Coffee => new Coffee(dto.Name, dto.Price, dto.Variant),
            ProductTypes.Tea => new Tea(dto.Name, dto.Price, dto.Variant),
            _ => new Cake(dto.Name, dto.Price, dto.Variant)
        };

        product.Id = dto.Id;
        product.Description = dto.Description;
        product.Emoji = dto.Emoji;
        product.IsAvailable = dto.IsAvailable;
        return product;
    }
}
