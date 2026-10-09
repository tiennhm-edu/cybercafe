// ============================================================================
// AddToCartArgs.cs — dữ liệu sự kiện "thêm vào giỏ" (Buổi 24–31 · EventCallback<T>).
// EventCallback chỉ truyền được 1 tham số → gom Product + Size + Quantity vào 1 record.
// ============================================================================
using CyberCafe.Domain.Products;

namespace CyberCafe.Web.Models;

/// <summary>
/// Dữ liệu ProductCard gửi lên component cha qua EventCallback&lt;AddToCartArgs&gt;.
/// <c>record</c> với primary constructor: C# tự sinh property chỉ đọc (init) + Equals/ToString — gọn cho "gói dữ liệu".
/// </summary>
public record AddToCartArgs(Product Product, DrinkSize Size, int Quantity);
