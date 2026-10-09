// ============================================================================
// IRequest.cs — "thông điệp" gửi qua dispatcher (Buổi 51 · CQRS).
// CQRS = Command Query Responsibility Segregation: tách việc GHI (command) khỏi việc ĐỌC (query).
//   Command: "hãy làm X" — đổi trạng thái hệ thống, có thể bị TỪ CHỐI (PlaceOrder, CancelOrder...).
//   Query:   "cho tôi biết Y" — KHÔNG đổi gì, gọi 100 lần cũng như 1 lần (GetMenu, GetOrderById...).
// Kiểu trả về nằm NGAY trong interface (IRequest<OrderDto>) → sender.Send(cmd) biết kiểu kết quả lúc biên dịch.
// Tự viết thay vì MediatR (MediatR 13+ là giấy phép thương mại) — lý do đầy đủ: docs/adr/0003-cqrs-dispatcher-tu-viet.md.
// ============================================================================
namespace CyberCafe.Application.Common.Messaging;

// 👉 Bước 1 (b51.md)
/// <summary>Thông điệp có kết quả kiểu <typeparamref name="TResponse"/>. Không implement trực tiếp — dùng ICommand/IQuery.</summary>
/// <typeparam name="TResponse">Kiểu kết quả handler trả về.</typeparam>
public interface IRequest<TResponse>;

/// <summary>Đánh dấu "đây là command" (không generic) — TransactionBehavior dùng để nhận ra command.</summary>
public interface ICommandBase;

/// <summary>Yêu cầu GHI: đổi trạng thái hệ thống, chạy trong transaction (Buổi 52).</summary>
/// <typeparam name="TResponse">Kết quả trả về sau khi ghi (vd OrderDto vừa tạo).</typeparam>
public interface ICommand<TResponse> : IRequest<TResponse>, ICommandBase;

/// <summary>Yêu cầu ĐỌC: không đổi dữ liệu, không cần transaction.</summary>
/// <typeparam name="TResponse">Dữ liệu trả về.</typeparam>
public interface IQuery<TResponse> : IRequest<TResponse>;
