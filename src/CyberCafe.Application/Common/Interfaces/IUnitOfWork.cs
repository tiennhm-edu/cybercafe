// ============================================================================
// IUnitOfWork.cs — "lưu mọi thay đổi trong 1 giao dịch" (Buổi 48 · 52 · port của Application).
// ADR 0001 (b47): DbContext ĐÃ là Unit of Work → không bọc thêm.
// ADR 0002 (b48): Application không được tham chiếu EF Core → không gọi được db.SaveChangesAsync()
//   → cần 1 interface TỐI GIẢN. CyberCafeDbContext (Infrastructure) implement luôn interface này:
//   method SaveChangesAsync(CancellationToken) của DbContext có sẵn đúng chữ ký → không viết thêm dòng nào.
// Buổi 52: thêm ExecuteInTransactionAsync cho TransactionBehavior (transaction tường minh quanh cả command).
// ============================================================================
namespace CyberCafe.Application.Common.Interfaces;

/// <summary>Ghi các thay đổi đang theo dõi xuống database trong 1 transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Lưu mọi thay đổi (INSERT/UPDATE/DELETE gom thành 1 transaction). Trả về số dòng bị ảnh hưởng.
    /// Buổi 50: lưu xong thì phát domain event của các aggregate (hoặc chờ tới khi transaction ngoài commit).
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // 👉 Bước 4 (b52.md)
    /// <summary>
    /// Chạy <paramref name="work"/> trong 1 transaction: thành công → commit rồi mới phát domain event;
    /// lỗi → rollback, bỏ các event đang chờ. Database không hỗ trợ transaction (EF InMemory) → chạy thẳng.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
