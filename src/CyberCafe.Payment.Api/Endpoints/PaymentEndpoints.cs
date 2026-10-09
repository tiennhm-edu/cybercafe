// ============================================================================
// PaymentEndpoints.cs — minimal API của Payment service (Buổi 54 · minimal API so với controller).
// Service nhỏ, 2 endpoint chỉ đọc → minimal API gọn hơn controller (không cần class, attribute, ModelState).
// Ai được gọi? Request đi qua GATEWAY: route /payments/* có AuthorizationPolicy "AdminOnly" (kiểm JWT ở gateway).
// Service này không tự kiểm JWT: dưới AppHost nó không mở cổng ra ngoài (chỉ gateway + web là external).
// ⚠️ Môi trường thật: vẫn nên kiểm JWT ở cả service ("defense in depth") hoặc dùng mạng nội bộ / mTLS —
//    "chỉ gateway mới gọi được" là giả định về HẠ TẦNG, không phải về code. Xem b54.md, mục Gateway + JWT.
// ============================================================================
using CyberCafe.Payment.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CyberCafe.Payment.Api.Endpoints;

/// <summary>Dữ liệu 1 giao dịch trả cho client (không lộ entity EF ra ngoài).</summary>
/// <param name="OrderId">Id đơn bên Order service.</param>
/// <param name="OrderCode">Mã đơn.</param>
/// <param name="Amount">Số tiền.</param>
/// <param name="Method">Hình thức.</param>
/// <param name="CardLast4">4 số cuối thẻ.</param>
/// <param name="Status">"Succeeded" / "Failed".</param>
/// <param name="FailureReason">Lý do từ chối.</param>
/// <param name="TransactionId">Mã giao dịch.</param>
/// <param name="ProcessedAtUtc">Thời điểm (UTC).</param>
public sealed record PaymentView(
    int OrderId, string OrderCode, decimal Amount, string Method, string? CardLast4,
    string Status, string? FailureReason, string TransactionId, DateTime ProcessedAtUtc);

// 👉 Bước 2 (b54.md)
/// <summary>Đăng ký endpoint /payments.</summary>
public static class PaymentEndpoints
{
    /// <summary>GET /payments?take=20 và GET /payments/orders/{orderId}.</summary>
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // MapGroup: chung tiền tố "/payments" — giống [Route("api/[controller]")] của controller
        RouteGroupBuilder group = app.MapGroup("/payments");

        group.MapGet("/", async (PaymentDbContext db, int? take, CancellationToken ct) =>
        {
            int size = Math.Clamp(take ?? 20, 1, 100); // kẹp kích thước trang như b40
            List<PaymentView> items = await db.Transactions.AsNoTracking()
                .OrderByDescending(t => t.ProcessedAtUtc)
                .Take(size)
                .Select(t => new PaymentView(t.OrderId, t.OrderCode, t.Amount, t.Method, t.CardLast4,
                    t.Status.ToString(), t.FailureReason, t.TransactionId, t.ProcessedAtUtc))
                .ToListAsync(ct);
            return TypedResults.Ok(items);
        });

        group.MapGet("/orders/{orderId:int}", async Task<Results<Ok<PaymentView>, NotFound>> (int orderId, PaymentDbContext db, CancellationToken ct) =>
        {
            PaymentView? view = await db.Transactions.AsNoTracking()
                .Where(t => t.OrderId == orderId)
                .Select(t => new PaymentView(t.OrderId, t.OrderCode, t.Amount, t.Method, t.CardLast4,
                    t.Status.ToString(), t.FailureReason, t.TransactionId, t.ProcessedAtUtc))
                .SingleOrDefaultAsync(ct);
            // Results<Ok<T>, NotFound>: kiểu trả về liệt kê đủ các khả năng → OpenAPI và người đọc code đều thấy
            return view is null ? TypedResults.NotFound() : TypedResults.Ok(view);
        });

        return app;
    }
}
