// ============================================================================
// TransactionBehavior.cs — bọc mỗi COMMAND trong 1 transaction database (Buổi 52 · transaction).
// Query không đổi dữ liệu → đi thẳng (mở transaction cho query chỉ tốn khóa vô ích).
// "SaveChanges đã là 1 transaction rồi mà?" — đúng, nếu handler chỉ gọi SaveChanges 1 lần. Behavior này lo
// trường hợp handler gọi NHIỀU lần (hoặc gọi thêm SQL thô): tất cả thành công hoặc tất cả quay lui.
// Thêm 1 việc quan trọng (b50 + b52): domain event được giữ lại tới khi COMMIT xong mới phát
// → không bao giờ báo barista 1 đơn "ma" đã bị rollback (xem CyberCafeDbContext.ExecuteInTransactionAsync).
// ============================================================================
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;

namespace CyberCafe.Application.Common.Behaviors;

// 👉 Bước 4 (b52.md)
/// <summary>Command → chạy trong IUnitOfWork.ExecuteInTransactionAsync; query → bỏ qua.</summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Kiểm tra lúc CHẠY thay vì ràng buộc generic "where TRequest : ICommandBase": DI của .NET đăng ký
        // open generic cho MỌI request, ràng buộc không khớp thì resolve sẽ lỗi → đơn giản và chắc ăn hơn.
        if (request is not ICommandBase)
        {
            return next(ct);
        }

        return unitOfWork.ExecuteInTransactionAsync(token => next(token), ct);
    }
}
