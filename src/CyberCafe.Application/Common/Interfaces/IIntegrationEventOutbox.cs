// ============================================================================
// IIntegrationEventOutbox.cs — port "ghi message vào OUTBOX" (Buổi 55 · Outbox pattern).
// Vấn đề "dual write": lưu đơn vào SQL Server VÀ gửi message lên RabbitMQ là 2 hệ thống khác nhau,
// không có transaction chung. Lưu xong mà sập trước khi gửi → Payment không bao giờ biết có đơn;
// gửi trước rồi lưu lỗi → Payment thu tiền 1 đơn "ma".
// Outbox: KHÔNG gửi thẳng. Ghi message thành 1 DÒNG trong bảng OutboxMessages, CÙNG transaction với đơn
// → hoặc cả 2 cùng có, hoặc cả 2 cùng không. 1 tiến trình nền (OutboxDispatcher) đọc bảng và gửi lên broker sau.
// Application chỉ biết interface này — không biết EF Core, RabbitMQ hay MassTransit (architecture test kiểm tra).
// ============================================================================
using CyberCafe.IntegrationEvents;

namespace CyberCafe.Application.Common.Interfaces;

// 👉 Bước 2 (b55.md)
/// <summary>Xếp integration event vào outbox; được lưu ở lần <see cref="IUnitOfWork.SaveChangesAsync"/> kế tiếp.</summary>
public interface IIntegrationEventOutbox
{
    /// <summary>
    /// Thêm event vào outbox (chưa ghi DB ngay). Gọi TRONG command handler, trước SaveChanges —
    /// TransactionBehavior (b52) bảo đảm đơn + message cùng commit hoặc cùng rollback.
    /// </summary>
    void Enqueue(IIntegrationEvent integrationEvent);
}
