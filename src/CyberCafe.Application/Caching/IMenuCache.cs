// ============================================================================
// IMenuCache.cs — cổng (port) cache thực đơn (Buổi 42–47 · 48).
// Buổi 42–47: MenuCache là class cụ thể trong Api, controller + filter inject thẳng class đó.
// Buổi 48: use case MenuService cần cache nhưng KHÔNG được biết Redis / IDistributedCache là gì
//   → Application khai báo "cần gì" (interface này), Infrastructure lo "làm thế nào" (MenuCache + Redis).
// Đây là Dependency Inversion: mũi tên phụ thuộc chỉ VÀO Application, không chỉ ra hạ tầng.
// ============================================================================
namespace CyberCafe.Application.Caching;

/// <summary>Kết quả đọc qua cache: giá trị + có lấy từ cache không (để gắn header X-Cache).</summary>
/// <typeparam name="T">Kiểu dữ liệu.</typeparam>
/// <param name="Value">Giá trị.</param>
/// <param name="Hit">true = lấy từ cache.</param>
public record CacheResult<T>(T Value, bool Hit);

// 👉 Bước 5 (b48.md)
/// <summary>Cache-aside cho dữ liệu thực đơn, có vô hiệu hóa toàn bộ khi thực đơn đổi.</summary>
public interface IMenuCache
{
    /// <summary>
    /// Đọc qua cache. <paramref name="factory"/> chỉ được gọi khi MISS (hoặc cache lỗi).
    /// Kết quả null KHÔNG được cache.
    /// </summary>
    Task<CacheResult<T>> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);

    /// <summary>Vô hiệu hóa toàn bộ cache thực đơn (gọi sau mỗi lần thêm/sửa/xóa món).</summary>
    Task InvalidateAsync(CancellationToken ct = default);
}
