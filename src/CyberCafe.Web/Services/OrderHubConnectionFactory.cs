// ============================================================================
// OrderHubConnectionFactory.cs — tạo HubConnection tới /hubs/orders của Api (Buổi 33–34).
// Blazor SERVER: HubConnection chạy TRÊN MÁY CHỦ Web (là .NET client), không phải trong trình duyệt.
//   Trình duyệt ──(circuit SignalR của Blazor)──► Web ──(HubConnection)──► Api/hubs/orders
// Mỗi trang (Barista, OrderConfirmation) tự tạo 1 kết nối và tự Dispose khi rời trang.
// Buổi 42–47: hub đòi JWT → truyền hàm lấy token (AccessTokenProvider). SignalR gọi hàm này mỗi lần
// kết nối/kết nối lại → luôn lấy token CÒN HẠN từ AuthSession (tự refresh nếu cần).
// ============================================================================
using System.Text.Json.Serialization;
using CyberCafe.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace CyberCafe.Web.Services;

/// <summary>Đóng gói cấu hình HubConnection để các trang không lặp lại URL/JSON/reconnect.</summary>
/// <param name="apiBaseUrl">Địa chỉ gốc của Api (đọc từ cấu hình "ApiBaseUrl").</param>
public class OrderHubConnectionFactory(Uri apiBaseUrl)
{
    private readonly Uri _hubUrl = new(apiBaseUrl, OrderHubContract.Path.TrimStart('/'));

    /// <summary>Tạo kết nối MỚI (chưa Start). Người gọi chịu trách nhiệm DisposeAsync.</summary>
    /// <param name="accessTokenProvider">Hàm trả access token (vd AuthSession.GetAccessTokenAsync); null = không gửi token.</param>
    public HubConnection Create(Func<Task<string?>>? accessTokenProvider = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(this._hubUrl, options =>
            {
                // WebSocket không có header Authorization → client tự gửi ?access_token=... (Api đọc ở OnMessageReceived)
                options.AccessTokenProvider = accessTokenProvider;
            })
            // Mất mạng → tự thử lại sau 0s, 2s, 10s, 30s rồi bỏ cuộc (sự kiện Closed).
            // ⚠️ Lỗi hay gặp: quên dòng này → API restart 1 lần là màn hình barista "chết" tới khi F5.
            .WithAutomaticReconnect()
            // Enum dạng chuỗi — khớp AddJsonProtocol bên Api
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
    }
}
