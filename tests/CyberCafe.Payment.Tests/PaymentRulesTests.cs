// ============================================================================
// PaymentRulesTests.cs — luật thanh toán giả lập của Payment service (Buổi 55).
// Class thuần → test như bài OOP: không DI, không DB, không broker.
// 👉 Bước 4 (b55.md)
// ============================================================================
using CyberCafe.Payment.Api.Processing;
using Microsoft.Extensions.Options;

namespace CyberCafe.Payment.Tests;

public class PaymentRulesTests
{
    private static readonly PaymentRules Rules = new(Options.Create(new PaymentRulesOptions { MaxAmount = 500_000m, DeclinedCardSuffix = "0000" }));

    // Kiểm tra: trong hạn mức → chấp nhận với mọi hình thức (không phân biệt hoa thường); đúng bằng hạn mức vẫn được.
    [Theory]
    [InlineData(68000, "Cash", null)]
    [InlineData(500000, "card", "1234")]
    [InlineData(1, "Momo", null)]
    public void WithinLimit_IsApproved(decimal amount, string method, string? last4)
    {
        PaymentDecision decision = Rules.Decide(amount, method, last4);

        Assert.True(decision.Approved);
        Assert.Null(decision.Reason);
    }

    // Kiểm tra: các nhánh từ chối — vượt hạn mức, thẻ đuôi 0000, thẻ thiếu 4 số cuối, hình thức lạ, số tiền âm.
    [Theory]
    [InlineData(500001, "Cash", null, "Vượt hạn mức")]
    [InlineData(68000, "Card", "0000", "Ngân hàng từ chối")]
    [InlineData(68000, "Card", null, "Thiếu thông tin thẻ")]
    [InlineData(68000, "Bitcoin", null, "không hợp lệ")]
    [InlineData(-1, "Cash", null, "không hợp lệ")]
    public void Violations_AreDeclinedWithReason(decimal amount, string method, string? last4, string reasonPart)
    {
        PaymentDecision decision = Rules.Decide(amount, method, last4);

        Assert.False(decision.Approved);
        Assert.Contains(reasonPart, decision.Reason);
    }
}
