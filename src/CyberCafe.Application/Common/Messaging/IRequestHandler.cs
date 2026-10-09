// ============================================================================
// IRequestHandler.cs — mỗi command/query có ĐÚNG 1 handler (Buổi 51 · CQRS).
// Handler = 1 use case. So với b48 (OrderService có 7 method): mỗi use case 1 class nhỏ,
// constructor chỉ inject đúng thứ use case đó cần → đọc/test/sửa từng use case độc lập.
// ============================================================================
namespace CyberCafe.Application.Common.Messaging;

// 👉 Bước 2 (b51.md)
/// <summary>Xử lý 1 loại request. Dispatcher (Sender) tìm handler qua DI theo kiểu request.</summary>
/// <typeparam name="TRequest">Kiểu command/query.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Thực thi use case.</summary>
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}

/// <summary>Handler của command (chỉ là tên gọi rõ nghĩa hơn — vẫn là IRequestHandler).</summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

/// <summary>Handler của query.</summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
