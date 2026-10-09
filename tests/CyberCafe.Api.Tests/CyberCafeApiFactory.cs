// ============================================================================
// CyberCafeApiFactory.cs — dựng CyberCafe.Api trong bộ nhớ cho test tích hợp (Buổi 36–41).
// 👉 Bước 13 (b40.md): WebApplicationFactory<Program> chạy NGUYÊN Program.cs (routing, controller, filter, JSON...)
// trên TestServer (không mở cổng mạng). Ta chỉ thay 2 thứ:
//   1) SQL Server → EF Core InMemory (mỗi factory 1 database riêng, tên ngẫu nhiên).
//   2) IOrderNotifier → bản ghi lại (để kiểm tra "đã báo barista chưa") — trừ khi test hub thật.
// ⚠️ InMemory KHÔNG phải SQL thật: không kiểm tra khóa ngoại, Contains phân biệt hoa/thường,
//    không chạy được SQL thô (stored procedure) → phần SP được kiểm thử tay với Docker (docs/sessions/b40.md).
// ============================================================================
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberCafe.Api.Data;
using CyberCafe.Api.Realtime;
using CyberCafe.Contracts.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CyberCafe.Api.Tests;

/// <summary>Factory dùng chung cho mọi test tích hợp của Api.</summary>
public class CyberCafeApiFactory : WebApplicationFactory<Program>
{
    /// <summary>JSON giống Api/Web: camelCase + enum dạng chuỗi.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _databaseName = $"CyberCafeTests-{Guid.NewGuid()}";

    /// <summary>true = giữ SignalROrderNotifier thật (test hub); false = dùng bản ghi lại.</summary>
    public bool UseRealNotifier { get; init; }

    /// <summary>Các sự kiện đã "gửi" (khi không dùng notifier thật).</summary>
    public RecordingOrderNotifier Notifier { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Gỡ cấu hình UseSqlServer đã đăng ký trong Program.cs.
            // EF Core 9+: cấu hình provider nằm trong IDbContextOptionsConfiguration<T> → phải gỡ cả nó,
            // nếu không sẽ lỗi "Services for database providers 'SqlServer', 'InMemory' have been registered".
            services.RemoveAll<DbContextOptions<CyberCafeDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CyberCafeDbContext>>();
            services.AddDbContext<CyberCafeDbContext>(options => options.UseInMemoryDatabase(this._databaseName));

            if (!this.UseRealNotifier)
            {
                services.RemoveAll<IOrderNotifier>();
                services.AddSingleton<IOrderNotifier>(this.Notifier);
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        IHost host = base.CreateHost(builder);

        // EnsureCreated: tạo "database" InMemory theo model + nạp dữ liệu HasData (8 món mẫu).
        // (Với SQL Server thật ta dùng migration; InMemory không có migration.)
        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CyberCafeDbContext>().Database.EnsureCreated();
        return host;
    }

    /// <summary>Chạy 1 đoạn code với DbContext thật của app (đọc/ghi thẳng DB trong test).</summary>
    public async Task WithDbAsync(Func<CyberCafeDbContext, Task> action)
    {
        using IServiceScope scope = this.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<CyberCafeDbContext>());
    }
}

/// <summary>IOrderNotifier giả: chỉ ghi lại đơn nào đã được thông báo.</summary>
public class RecordingOrderNotifier : IOrderNotifier
{
    /// <summary>Đơn đã báo "OrderPlaced".</summary>
    public ConcurrentQueue<OrderDto> Placed { get; } = new();

    /// <summary>Đơn đã báo "OrderStatusChanged".</summary>
    public ConcurrentQueue<OrderDto> StatusChanged { get; } = new();

    public Task OrderPlacedAsync(OrderDto order, CancellationToken ct = default)
    {
        this.Placed.Enqueue(order);
        return Task.CompletedTask;
    }

    public Task OrderStatusChangedAsync(OrderDto order, CancellationToken ct = default)
    {
        this.StatusChanged.Enqueue(order);
        return Task.CompletedTask;
    }
}
