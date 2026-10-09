// ============================================================================
// ApiClientBase.cs — phần dùng chung của mọi typed HttpClient gọi CyberCafe.Api (Buổi 32–35).
// Gom 3 việc lặp lại vào 1 chỗ:
//   1) JSON options giống API (camelCase + enum dạng chuỗi).
//   2) Lỗi mạng (API chưa chạy, timeout) → ApiException với thông báo tiếng Việt.
//   3) Response 4xx/5xx → đọc ProblemDetails (title, detail, errors) → ApiException.
// Trang Razor chỉ cần: try { await Client.XxxAsync(); } catch (ApiException ex) { _error = ex.Message; }
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberCafe.Web.Services;

/// <summary>Lỗi khi gọi API — đã chuyển thành thông báo đọc được cho người dùng.</summary>
/// <param name="status">Mã HTTP (503 nếu không kết nối được).</param>
/// <param name="message">Thông báo tiếng Việt.</param>
/// <param name="inner">Exception gốc (nếu có).</param>
public class ApiException(HttpStatusCode status, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Mã HTTP của response (hoặc 503/504 khi lỗi mạng).</summary>
    public HttpStatusCode Status { get; } = status;
}

/// <summary>Lớp cha của MenuApiClient, OrderApiClient.</summary>
public abstract class ApiClientBase(HttpClient http)
{
    /// <summary>
    /// JSON giống phía Api: camelCase (JsonSerializerDefaults.Web) + enum dạng chuỗi ("Pending").
    /// ⚠️ Lỗi hay gặp: API trả "status": "Pending" mà client thiếu JsonStringEnumConverter → JsonException.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>HttpClient do IHttpClientFactory cấp (đã có BaseAddress = ApiBaseUrl).</summary>
    protected HttpClient Http => http;

    /// <summary>Gửi request, đọc body JSON thành <typeparamref name="T"/>.</summary>
    protected async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using HttpResponseMessage response = await this.SendCoreAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    /// <summary>Gửi request không cần đọc body (DELETE → 204).</summary>
    protected async Task SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using HttpResponseMessage response = await this.SendCoreAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    /// <summary>Tạo request có body JSON (POST/PUT).</summary>
    protected static HttpRequestMessage JsonRequest<TBody>(HttpMethod method, string url, TBody body) =>
        new(method, url) { Content = JsonContent.Create(body, options: Json) };

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            // API chưa chạy / sai cổng / Docker SQL chưa bật... → báo rõ thay vì trang trắng
            throw new ApiException(HttpStatusCode.ServiceUnavailable,
                "Không kết nối được CyberCafe.Api. Hãy chạy: dotnet run --project src/CyberCafe.Api", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // TaskCanceled mà KHÔNG phải do ta hủy → hết thời gian chờ (HttpClient.Timeout)
            throw new ApiException(HttpStatusCode.GatewayTimeout, "API phản hồi quá lâu, vui lòng thử lại.", ex);
        }
    }

    // Đọc ProblemDetails (RFC 9457) mà ASP.NET Core trả về khi lỗi:
    //   { "title": "...", "status": 400, "detail": "...", "errors": { "Name": ["..."] } }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message;
        try
        {
            ProblemInfo? problem = await response.Content.ReadFromJsonAsync<ProblemInfo>(Json, ct);
            // ValidationProblem: gom mọi lỗi của từng field thành 1 chuỗi để hiện trong alert
            string? errors = problem?.Errors is { Count: > 0 }
                ? string.Join(" ", problem.Errors.SelectMany(e => e.Value))
                : null;
            message = errors ?? problem?.Detail ?? problem?.Title ?? $"Lỗi {(int)response.StatusCode}";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Body rỗng hoặc không phải JSON (vd 502 từ proxy)
            message = $"Lỗi {(int)response.StatusCode} {response.ReasonPhrase}";
        }

        throw new ApiException(response.StatusCode, message);
    }

    private sealed record ProblemInfo(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
