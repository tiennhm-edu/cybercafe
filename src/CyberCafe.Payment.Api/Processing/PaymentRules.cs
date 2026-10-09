// ============================================================================
// PaymentRules.cs — luật quyết định "thu được tiền hay không" (Buổi 55 · giả lập cổng thanh toán).
// Không gọi ngân hàng thật. 2 luật đủ để demo cả 2 nhánh của luồng sự kiện:
//   1) Số tiền > Payment:MaxAmount            → từ chối "Vượt hạn mức" (áp cho mọi hình thức)
//   2) Thẻ có 4 số cuối = DeclinedCardSuffix  → từ chối "Ngân hàng từ chối thẻ"
// Class thuần (không DbContext, không MassTransit) → unit test không cần gì cả (PaymentRulesTests).
// ============================================================================
using System.Globalization;
using Microsoft.Extensions.Options;

namespace CyberCafe.Payment.Api.Processing;

/// <summary>Cấu hình luật (section "Payment" trong appsettings.json).</summary>
public sealed class PaymentRulesOptions
{
    /// <summary>Tên section cấu hình.</summary>
    public const string SectionName = "Payment";

    /// <summary>Hạn mức 1 đơn (VND).</summary>
    public decimal MaxAmount { get; set; } = 500_000m;

    /// <summary>4 số cuối thẻ luôn bị từ chối.</summary>
    public string DeclinedCardSuffix { get; set; } = "0000";

    /// <summary>Thời gian chờ giả lập mỗi giao dịch (ms).</summary>
    public int SimulatedDelayMs { get; set; }
}

/// <summary>Kết quả quyết định.</summary>
/// <param name="Approved">Thu được tiền.</param>
/// <param name="Reason">Lý do từ chối (null khi Approved).</param>
public sealed record PaymentDecision(bool Approved, string? Reason)
{
    /// <summary>Chấp nhận.</summary>
    public static PaymentDecision Approve() => new(true, null);

    /// <summary>Từ chối kèm lý do.</summary>
    public static PaymentDecision Decline(string reason) => new(false, reason);
}

// 👉 Bước 4 (b55.md)
/// <summary>Luật thanh toán giả lập.</summary>
public sealed class PaymentRules(IOptions<PaymentRulesOptions> options)
{
    private static readonly string[] KnownMethods = ["Cash", "Card", "Momo"];

    /// <summary>Quyết định cho 1 yêu cầu thanh toán.</summary>
    /// <param name="amount">Số tiền.</param>
    /// <param name="method">"Cash" / "Card" / "Momo" (không phân biệt hoa thường).</param>
    /// <param name="cardLast4">4 số cuối thẻ (bắt buộc khi Card).</param>
    public PaymentDecision Decide(decimal amount, string method, string? cardLast4)
    {
        PaymentRulesOptions rules = options.Value;

        // Message đến từ service khác vẫn phải kiểm tra — đừng tin dữ liệu chỉ vì "nó từ hệ thống mình"
        if (amount < 0 || !KnownMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return PaymentDecision.Decline("Yêu cầu thanh toán không hợp lệ");
        }

        if (amount > rules.MaxAmount)
        {
            return PaymentDecision.Decline(
                $"Vượt hạn mức thanh toán trực tuyến ({rules.MaxAmount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đ)");
        }

        if (string.Equals(method, "Card", StringComparison.OrdinalIgnoreCase))
        {
            if (cardLast4 is not { Length: 4 })
            {
                return PaymentDecision.Decline("Thiếu thông tin thẻ");
            }

            if (cardLast4 == rules.DeclinedCardSuffix)
            {
                return PaymentDecision.Decline("Ngân hàng từ chối thẻ");
            }
        }

        return PaymentDecision.Approve();
    }
}
