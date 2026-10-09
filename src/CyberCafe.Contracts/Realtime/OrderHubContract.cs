// ============================================================================
// OrderHubContract.cs — "hợp đồng" SignalR giữa Api (OrderHub) và Web (HubConnection) (Buổi 33–34).
// SignalR gọi method theo TÊN dạng chuỗi. Gõ sai 1 chữ ("OrderPlace" thay vì "OrderPlaced")
// → không lỗi biên dịch, không exception, chỉ là... không bao giờ nhận được sự kiện.
// Gom tên vào hằng số dùng chung 2 phía để compiler bắt lỗi giúp ta.
// ============================================================================
namespace CyberCafe.Contracts.Realtime;

/// <summary>Đường dẫn hub, tên sự kiện server → client, tên method client → server, tên group.</summary>
public static class OrderHubContract
{
    /// <summary>URL của hub trên Api: app.MapHub&lt;OrderHub&gt;(Path).</summary>
    public const string Path = "/hubs/orders";

    // ----- Server → Client (client đăng ký bằng connection.On<OrderDto>(...)) -----

    /// <summary>Có đơn mới (gửi tới group baristas).</summary>
    public const string OrderPlaced = "OrderPlaced";

    /// <summary>Đơn đổi trạng thái (gửi tới group baristas và group order-{id}).</summary>
    public const string OrderStatusChanged = "OrderStatusChanged";

    // ----- Client → Server (client gọi bằng connection.InvokeAsync(...)) -----

    /// <summary>Vào group quầy barista để nhận mọi đơn mới.</summary>
    public const string JoinBaristas = "JoinBaristas";

    /// <summary>Theo dõi 1 đơn cụ thể (trang xác nhận đơn của khách).</summary>
    public const string WatchOrder = "WatchOrder";

    // ----- Tên group -----

    /// <summary>Group của mọi màn hình quầy pha chế.</summary>
    public const string BaristasGroup = "baristas";

    /// <summary>Group riêng của 1 đơn: chỉ ai đang xem đơn đó mới nhận cập nhật.</summary>
    public static string OrderGroup(int orderId) => $"order-{orderId}";
}
