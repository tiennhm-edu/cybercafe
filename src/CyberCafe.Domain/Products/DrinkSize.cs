// ============================================================================
// DrinkSize.cs — enum size đồ uống (Buổi 24–31).
// ============================================================================
namespace CyberCafe.Domain.Products;

/// <summary>
/// Size đồ uống. Khác day-04: Size không còn là property của Coffee
/// mà được chọn trên từng dòng đơn (OrderItem), vì 1 món trên menu bán nhiều size.
/// </summary>
public enum DrinkSize
{
    /// <summary>Nhỏ — giá gốc.</summary>
    S,

    /// <summary>Vừa — phụ thu <see cref="Drink.SizeMSurcharge"/>.</summary>
    M,

    /// <summary>Lớn — phụ thu <see cref="Drink.SizeLSurcharge"/>.</summary>
    L
}
