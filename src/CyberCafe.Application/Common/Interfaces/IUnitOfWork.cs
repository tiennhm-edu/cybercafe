// ============================================================================
// IUnitOfWork.cs — "lưu mọi thay đổi trong 1 giao dịch" (Buổi 48 · port của Application).
// ADR 0001 (b47): DbContext ĐÃ là Unit of Work → không bọc thêm.
// ADR 0002 (b48): Application không được tham chiếu EF Core → không gọi được db.SaveChangesAsync()
//   → cần 1 interface TỐI GIẢN. CyberCafeDbContext (Infrastructure) implement luôn interface này:
//   method SaveChangesAsync(CancellationToken) của DbContext có sẵn đúng chữ ký → không viết thêm dòng nào.
// ============================================================================
namespace CyberCafe.Application.Common.Interfaces;

/// <summary>Ghi các thay đổi đang theo dõi xuống database trong 1 transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Lưu mọi thay đổi (INSERT/UPDATE/DELETE gom thành 1 transaction). Trả về số dòng bị ảnh hưởng.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
