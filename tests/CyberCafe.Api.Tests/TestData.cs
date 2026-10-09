// ============================================================================
// TestData.cs — dữ liệu mẫu dùng lại giữa các test (Buổi 32–41).
// Id món khớp MenuSeed: 1 Cà phê sữa đá 29k, 3 Americano 35k, 4 Trà đào 39k, 6 Matcha (tạm hết), 7 Tiramisu 35k.
// ============================================================================
using System.Net.Http.Json;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;

namespace CyberCafe.Api.Tests;

/// <summary>Request mẫu + helper gọi API.</summary>
public static class TestData
{
    /// <summary>Đơn hợp lệ: 2 cà phê sữa đá size M (2 × 34.000) + 1 tiramisu (35.000) = 103.000.</summary>
    public static PlaceOrderRequest Order(string? discountCode = null, params OrderLineRequest[] lines) => new()
    {
        CustomerName = "Nguyễn Văn An",
        PhoneNumber = "0901234567",
        Items = lines.Length > 0 ? [.. lines] : [new OrderLineRequest(1, DrinkSize.M, 2), new OrderLineRequest(7, DrinkSize.L, 1)],
        DiscountCode = discountCode,
        PaymentMethod = PaymentMethod.Cash,
        Note = "Ít đá",
    };

    /// <summary>POST /api/orders và đọc OrderDto (ném nếu không phải 2xx).</summary>
    public static async Task<OrderDto> PlaceAsync(this HttpClient client, PlaceOrderRequest? request = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/orders", request ?? Order(), CyberCafeApiFactory.Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(CyberCafeApiFactory.Json))!;
    }

    /// <summary>Đọc body JSON với options của test (enum dạng chuỗi).</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(CyberCafeApiFactory.Json))!;
}
