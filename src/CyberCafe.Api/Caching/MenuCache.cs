// ============================================================================
// MenuCache.cs — cache thực đơn bằng IDistributedCache (Redis) (Buổi 42–47 · cache-aside + invalidation).
// Cache-aside: đọc cache → có (HIT) thì trả; không có (MISS) → đọc DB → ghi cache kèm TTL → trả.
// Vấn đề: GET /api/products có VÔ SỐ biến thể query (?page=2&type=tea...) → mỗi biến thể 1 key.
//   Khi admin sửa món, xóa từng key là không thể (IDistributedCache không có "xóa theo mẫu").
// Giải pháp: KEY CÓ PHIÊN BẢN. Mọi key có dạng "menu:v{version}:..."; ghi dữ liệu → đổi version
//   → toàn bộ key cũ thành "mồ côi" (không ai đọc nữa) và tự hết hạn theo TTL.
// Redis lỗi/tắt → KHÔNG làm sập API: log cảnh báo, đọc thẳng DB, tạm bỏ qua cache 30 giây.
// ============================================================================
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace CyberCafe.Api.Caching;

/// <summary>Kết quả đọc qua cache: giá trị + có lấy từ cache không (để gắn header X-Cache).</summary>
/// <typeparam name="T">Kiểu dữ liệu.</typeparam>
/// <param name="Value">Giá trị.</param>
/// <param name="Hit">true = lấy từ cache.</param>
public record CacheResult<T>(T Value, bool Hit);

/// <summary>Cache dữ liệu thực đơn (Singleton — IDistributedCache cũng Singleton).</summary>
public class MenuCache(IDistributedCache cache, TimeProvider clock, ILogger<MenuCache> logger)
{
    private const string VersionKey = "menu:version";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BypassAfterFailure = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Đơn giản hóa "circuit breaker": Redis vừa lỗi → bỏ qua cache 1 lúc, khỏi request nào cũng chờ timeout.
    // Singleton dùng chung nhiều request → đọc/ghi field qua Interlocked/volatile cho an toàn đa luồng.
    private long _bypassUntilTicks;

    // 👉 Bước 8 (b47.md)
    /// <summary>
    /// Đọc qua cache. <paramref name="factory"/> chỉ được gọi khi MISS (hoặc Redis lỗi).
    /// Kết quả null (vd món không tồn tại) KHÔNG được cache — tránh giữ 404 sai sau khi admin tạo món.
    /// </summary>
    public async Task<CacheResult<T>> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default)
    {
        if (this.IsBypassed)
        {
            return new CacheResult<T>(await factory(ct), Hit: false);
        }

        string fullKey;
        try
        {
            fullKey = $"menu:v{await this.GetVersionAsync(ct)}:{key}";
            byte[]? cached = await cache.GetAsync(fullKey, ct);
            if (cached is not null)
            {
                return new CacheResult<T>(JsonSerializer.Deserialize<T>(cached, Json)!, Hit: true);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Trip(ex, "đọc");
            return new CacheResult<T>(await factory(ct), Hit: false);
        }

        T value = await factory(ct);
        if (value is not null)
        {
            try
            {
                await cache.SetAsync(fullKey, JsonSerializer.SerializeToUtf8Bytes(value, Json),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.Trip(ex, "ghi");
            }
        }

        return new CacheResult<T>(value, Hit: false);
    }

    /// <summary>
    /// Vô hiệu hóa TOÀN BỘ cache thực đơn bằng cách đổi version (gọi sau mỗi lần thêm/sửa/xóa món).
    /// Quy tắc vàng: GHI xong → XÓA/VÔ HIỆU cache; không bao giờ tự "sửa" dữ liệu bên trong cache.
    /// </summary>
    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try
        {
            await cache.SetStringAsync(VersionKey, Guid.NewGuid().ToString("N"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ⚠️ Redis tắt đúng lúc admin sửa giá → version không đổi được; khi Redis sống lại, dữ liệu cũ
            //    còn tối đa 1 TTL (5 phút). TTL chính là "lưới an toàn" cho trường hợp này.
            this.Trip(ex, "vô hiệu hóa");
        }
    }

    private async Task<string> GetVersionAsync(CancellationToken ct)
    {
        string? version = await cache.GetStringAsync(VersionKey, ct);
        if (version is null)
        {
            version = Guid.NewGuid().ToString("N");
            await cache.SetStringAsync(VersionKey, version, ct); // không TTL: version sống tới khi bị đổi
        }

        return version;
    }

    private bool IsBypassed => clock.GetUtcNow().UtcTicks < Interlocked.Read(ref this._bypassUntilTicks);

    private void Trip(Exception ex, string action)
    {
        Interlocked.Exchange(ref this._bypassUntilTicks, clock.GetUtcNow().Add(BypassAfterFailure).UtcTicks);
        logger.LogWarning(ex, "Cache thực đơn lỗi khi {Action} — đọc thẳng database trong {Seconds}s", action, BypassAfterFailure.TotalSeconds);
    }
}
