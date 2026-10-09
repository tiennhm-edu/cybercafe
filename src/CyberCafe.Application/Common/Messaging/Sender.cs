// ============================================================================
// Sender.cs — DISPATCHER tự viết: nhận command/query → tìm handler + behavior qua DI → chạy (Buổi 51–52).
// Controller chỉ cần 1 dependency: ISender. Thêm use case mới = thêm 1 record + 1 handler, không sửa controller DI.
// Bài toán kỹ thuật: Send<TResponse>(IRequest<TResponse>) chỉ biết TResponse lúc biên dịch, còn TRequest
// (PlaceOrderCommand? CancelOrderCommand?) chỉ biết lúc chạy → không viết thẳng được IRequestHandler<TRequest, TResponse>.
// Cách gỡ (MediatR cũng làm vậy): tạo 1 "wrapper" generic theo ĐÚNG kiểu request bằng MakeGenericType
// MỘT LẦN rồi cache lại; từ đó trở đi gọi wrapper như code generic bình thường (không reflection mỗi lần).
// ============================================================================
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CyberCafe.Application.Common.Messaging;

// 👉 Bước 3 (b51.md)
/// <summary>Gửi command/query tới handler của nó.</summary>
public interface ISender
{
    /// <summary>Chạy pipeline (behavior → handler) cho <paramref name="request"/> và trả kết quả.</summary>
    /// <exception cref="InvalidOperationException">Chưa đăng ký handler cho kiểu request này.</exception>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}

/// <summary>Cài đặt ISender bằng DI của .NET (Scoped: dùng chung scope với DbContext của request).</summary>
public sealed class Sender(IServiceProvider services) : ISender
{
    // Kiểu request → wrapper đã dựng sẵn. static: dùng chung mọi request/scope (wrapper không giữ state).
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Lần đầu gặp PlaceOrderCommand: dựng RequestWrapper<PlaceOrderCommand, OrderDto>; các lần sau lấy từ cache
        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) => Activator.CreateInstance(
                typeof(RequestWrapper<,>).MakeGenericType(requestType, responseType))!,
            typeof(TResponse));

        return wrapper.Handle(request, services, ct);
    }
}

/// <summary>Phần không phụ thuộc TRequest — để Sender gọi được khi chỉ biết TResponse.</summary>
internal abstract class RequestWrapper<TResponse>
{
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
}

/// <summary>Biết ĐỦ cả 2 kiểu → resolve handler/behavior từ DI bằng code generic bình thường.</summary>
internal sealed class RequestWrapper<TRequest, TResponse> : RequestWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
    {
        TRequest typed = (TRequest)request;
        IRequestHandler<TRequest, TResponse> handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"Chưa đăng ký handler cho {typeof(TRequest).Name} (quên AddApplication()?)");

        // 👉 Bước 1 (b52.md): dựng "củ hành" từ TRONG ra NGOÀI. Behavior đăng ký ĐẦU TIÊN phải nằm NGOÀI CÙNG
        // → duyệt danh sách theo thứ tự NGƯỢC, mỗi vòng bọc thêm 1 lớp quanh "next" hiện tại.
        RequestHandlerDelegate<TResponse> next = token => handler.Handle(typed, token);
        foreach (IPipelineBehavior<TRequest, TResponse> behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            RequestHandlerDelegate<TResponse> inner = next; // ⚠️ copy ra biến riêng — lambda bắt biến "next" sẽ tự gọi chính nó (vòng lặp vô hạn)
            next = token => behavior.Handle(typed, inner, token);
        }

        return next(ct);
    }
}
