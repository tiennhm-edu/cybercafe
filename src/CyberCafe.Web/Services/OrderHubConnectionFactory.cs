// ============================================================================
// OrderHubConnectionFactory.cs — tạo HubConnection tới /hubs/orders của Api (Buổi 33–34).
// Blazor SERVER: HubConnection chạy TRÊN MÁY CHỦ Web (là .NET client), không phải trong trình duyệt.
//   Trình duyệt ──(circuit SignalR của Blazor)──► Web ──(HubConnection)──► Api/hubs/orders
// Mỗi trang (Barista, OrderConfirmation) tự tạo 1 kết nối và tự Dispose khi rời trang.
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
    public HubConnection Create()
    {
        return new HubConnectionBuilder()
            .WithUrl(this._hubUrl)
            // Mất mạng → tự thử lại sau 0s, 2s, 10s, 30s rồi bỏ cuộc (sự kiện Closed).
            // ⚠️ Lỗi hay gặp: quên dòng này → API restart 1 lần là màn hình barista "chết" tới khi F5.
            .WithAutomaticReconnect()
            // Enum dạng chuỗi — khớp AddJsonProtocol bên Api
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
    }
}
