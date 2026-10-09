// ============================================================================
// AuthTestExtensions.cs — tạo HttpClient đã đăng nhập theo vai trò (Buổi 42–47).
// Đăng nhập THẬT qua POST /api/auth/login bằng tài khoản dev được seed → token giống hệt người dùng thật.
// ============================================================================
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CyberCafe.Api.Auth;
using CyberCafe.Contracts.Auth;

namespace CyberCafe.Api.Tests;

/// <summary>Helper đăng nhập cho test tích hợp.</summary>
public static class AuthTestExtensions
{
    /// <summary>Đăng nhập và trả cặp token.</summary>
    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password = CyberCafeApiFactory.DevPassword)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<AuthResponse>();
    }

    /// <summary>HttpClient đã gắn sẵn "Authorization: Bearer ..." của tài khoản <paramref name="email"/>.</summary>
    public static async Task<HttpClient> CreateClientAsAsync(this CyberCafeApiFactory factory, string email)
    {
        HttpClient client = factory.CreateClient();
        AuthResponse auth = await client.LoginAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Admin seed sẵn.</summary>
    public static Task<HttpClient> AdminAsync(this CyberCafeApiFactory factory) => factory.CreateClientAsAsync(DevAccountSeeder.AdminEmail);

    /// <summary>Barista seed sẵn.</summary>
    public static Task<HttpClient> BaristaAsync(this CyberCafeApiFactory factory) => factory.CreateClientAsAsync(DevAccountSeeder.BaristaEmail);

    /// <summary>Khách seed sẵn.</summary>
    public static Task<HttpClient> CustomerAsync(this CyberCafeApiFactory factory) => factory.CreateClientAsAsync(DevAccountSeeder.CustomerEmail);

    /// <summary>Đăng ký 1 khách MỚI (để test IDOR giữa 2 khách) và trả client đã đăng nhập.</summary>
    public static async Task<HttpClient> NewCustomerAsync(this CyberCafeApiFactory factory, string email)
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "Khach12345", FullName = "Khách Thứ Hai" });
        response.EnsureSuccessStatusCode();
        AuthResponse auth = await response.ReadAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
