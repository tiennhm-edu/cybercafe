// ============================================================================
// FakeHttpHandler.cs — HttpMessageHandler giả cho unit test typed HttpClient (Buổi 32–35).
// HttpClient không gửi gì ra mạng: mọi request đi vào handler này, handler ghi lại request
// và trả response dựng sẵn → test MenuApiClient/OrderApiClient KHÔNG cần chạy Api thật.
// ============================================================================
using System.Net;
using System.Net.Http.Json;
using CyberCafe.Web.Services;

namespace CyberCafe.Tests.Web;

/// <summary>Handler giả: gọi <paramref name="responder"/> cho mỗi request và lưu lại request để kiểm tra.</summary>
public sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    /// <summary>Các request đã nhận (method, đường dẫn + query, body dạng chuỗi).</summary>
    public List<(HttpMethod Method, string PathAndQuery, string? Body, string? Bearer)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Đọc body NGAY (sau khi gửi xong HttpClient sẽ dispose content)
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        this.Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body, request.Headers.Authorization?.Parameter));
        return responder(request);
    }

    /// <summary>Response JSON với cùng JsonSerializerOptions của client (enum dạng chuỗi).</summary>
    public static HttpResponseMessage Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body, options: ApiClientBase.Json) };

    /// <summary>Response lỗi dạng ProblemDetails giống ASP.NET Core.</summary>
    public static HttpResponseMessage Problem(HttpStatusCode status, string title, string? detail = null, Dictionary<string, string[]>? errors = null) =>
        Json(new { title, status = (int)status, detail, errors }, status);

    /// <summary>Tạo HttpClient dùng handler giả, BaseAddress giống cấu hình thật.</summary>
    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://api.test/") };
}
