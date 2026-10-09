// ============================================================================
// Fakes.cs — bản giả của các port (Buổi 51–53 · unit test không cần database).
// Mỗi fake chỉ làm đúng việc test cần (lưu vào List/Dictionary, đếm số lần gọi). Tự viết vài chục dòng
// thay vì dùng thư viện mock: đọc test là hiểu ngay, không phải học cú pháp Setup/Verify.
// ============================================================================
using System.Collections.Concurrent;
using CyberCafe.Application.Common;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Orders;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Application.Products;
using CyberCafe.Contracts.Common;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Products;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using Microsoft.Extensions.Logging;

namespace CyberCafe.Application.Tests;

/// <summary>Dữ liệu mẫu dùng chung.</summary>
public static class Sample
{
    /// <summary>Cà phê sữa đá 29.000 (Id 1).</summary>
    public static Coffee CaPheSua() => new("Cà phê sữa đá", 29000, "Robusta") { Id = 1 };

    /// <summary>Tiramisu 35.000 (Id 7).</summary>
    public static Cake Tiramisu() => new("Bánh tiramisu", 35000, "Cà phê") { Id = 7 };

    /// <summary>Matcha đang tạm hết (Id 6).</summary>
    public static Tea Matcha() => new("Matcha latte", 45000, "Matcha") { Id = 6, IsAvailable = false };

    /// <summary>Đơn đã thanh toán của tài khoản <paramref name="ownerId"/>, Id gán giả như DB đã lưu.</summary>
    public static Order PaidOrder(int id, int? ownerId)
    {
        Order order = Order.Create(new Customer("An", "0901234567"), null, ownerId);
        order.AddItem(CaPheSua(), DrinkSize.M, 1);
        order.Pay(new CashPayment(0, 100000));
        order.ClearDomainEvents();
        // Id có private set (chỉ EF gán) → test dùng Reflection để giả lập "đã lưu DB"
        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, id);
        return order;
    }
}

/// <summary>IProductRepository trong bộ nhớ.</summary>
public sealed class FakeProductRepository(params Product[] products) : IProductRepository
{
    private readonly Dictionary<int, Product> _products = products.ToDictionary(p => p.Id);

    /// <summary>Số lần GetByIdsAsync được gọi (kiểm tra handler có chạy hay không).</summary>
    public int Lookups { get; private set; }

    public Task<PagedResult<ProductDto>> GetPageAsync(ProductQuery query, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<ProductDto>(this._products.Values.Select(p => p.ToDto()).ToList(), 1, 20, this._products.Count));

    public Task<ProductDto?> GetDtoAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(this._products.TryGetValue(id, out Product? p) ? p.ToDto() : null);

    public Task<Product?> FindAsync(int id, CancellationToken ct = default) => Task.FromResult(this._products.GetValueOrDefault(id));

    public Task<IReadOnlyDictionary<int, Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        this.Lookups++;
        IReadOnlyDictionary<int, Product> found = this._products.Where(kv => ids.Contains(kv.Key)).ToDictionary();
        return Task.FromResult(found);
    }

    public void Add(Product product) => this._products[product.Id] = product;

    public void Remove(Product product) => this._products.Remove(product.Id);

    public Task<bool> IsSoldAsync(int productId, CancellationToken ct = default) => Task.FromResult(false);
}

/// <summary>IOrderRepository trong bộ nhớ.</summary>
public sealed class FakeOrderRepository(params Order[] existing) : IOrderRepository
{
    /// <summary>Đơn đã có sẵn (theo Id).</summary>
    public Dictionary<int, Order> Stored { get; } = existing.ToDictionary(o => o.Id);

    /// <summary>Đơn handler vừa Add.</summary>
    public List<Order> Added { get; } = [];

    public Task<Order?> GetAsync(int id, CancellationToken ct = default) => Task.FromResult(this.Stored.GetValueOrDefault(id));

    public void Add(Order order) => this.Added.Add(order);
}

/// <summary>IUnitOfWork giả: chỉ đếm số lần lưu / số transaction.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>Số lần SaveChangesAsync.</summary>
    public int Saves { get; private set; }

    /// <summary>Số lần ExecuteInTransactionAsync.</summary>
    public int Transactions { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.Saves++;
        return Task.FromResult(1);
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        this.Transactions++;
        return work(ct);
    }
}

/// <summary>IOrderNotifier ghi lại các đơn đã "báo".</summary>
public sealed class RecordingNotifier : IOrderNotifier
{
    public ConcurrentQueue<OrderDto> Placed { get; } = new();

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

/// <summary>IOrderReadStore giả: ghi lại tham số query handler truyền xuống.</summary>
public sealed class FakeOrderReadStore : IOrderReadStore
{
    public (IReadOnlyCollection<OrderStatus> Statuses, int Page, int PageSize)? LastBoard { get; private set; }

    public Task<OrderDto?> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default) => Task.FromResult<OrderDto?>(null);

    public Task<PagedResult<OrderDto>> GetByOwnerAsync(int? ownerId, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<OrderDto>([], page, pageSize, 0));

    public Task<PagedResult<OrderDto>> GetByStatusAsync(IReadOnlyCollection<OrderStatus> statuses, int page, int pageSize, CancellationToken ct = default)
    {
        this.LastBoard = (statuses, page, pageSize);
        return Task.FromResult(new PagedResult<OrderDto>([], page, pageSize, 0));
    }
}

/// <summary>ILogger&lt;T&gt; ghi log vào 1 danh sách dùng chung (để test thứ tự log của behavior).</summary>
public sealed class ListLogger<T>(LogSink sink) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        sink.Lines.Enqueue($"{logLevel}: {formatter(state, exception)}");
}

/// <summary>Nơi chứa log chung cho mọi ListLogger&lt;T&gt;.</summary>
public sealed class LogSink
{
    public ConcurrentQueue<string> Lines { get; } = new();
}
