// ============================================================================
// DddCqrsRulesTests.cs — luật DDD + CQRS được máy kiểm tra (Buổi 49–53).
// LayerDependencyTests (b48) kiểm "ai được tham chiếu ai". File này kiểm "code bên trong có đúng mẫu không":
//   - Aggregate không có setter public (chỉ đổi qua method nghiệp vụ).
//   - Value object / domain event / command / query BẤT BIẾN.
//   - Mỗi command/query có ĐÚNG 1 handler.
// 👉 Bước 5 (b53.md)
// ============================================================================
using System.Reflection;
using System.Runtime.CompilerServices;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;

namespace CyberCafe.ArchitectureTests;

public class DddCqrsRulesTests
{
    private static readonly Assembly Domain = typeof(Order).Assembly;
    private static readonly Assembly Application = typeof(ISender).Assembly;

    // Setter "ghi được từ bên ngoài": public set thường. init (record/with) KHÔNG tính — chỉ gán được lúc tạo object.
    private static bool HasPublicMutableSetter(PropertyInfo property) =>
        property.SetMethod is { IsPublic: true } setter
        && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static string[] MutableProperties(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(HasPublicMutableSetter)
        .Select(p => $"{type.Name}.{p.Name}")
        .ToArray();

    // Kiểm tra (b49): aggregate Order và entity con OrderItem không có setter public — muốn đổi phải gọi method nghiệp vụ.
    [Theory]
    [InlineData(typeof(Order))]
    [InlineData(typeof(OrderItem))]
    public void Aggregate_HasNoPublicSetters(Type type)
    {
        Assert.Empty(MutableProperties(type));
    }

    // Kiểm tra (b49): mọi aggregate root kế thừa AggregateRoot và KHÔNG có constructor public
    // (tạo qua factory method như Order.Create → luôn bắt đầu ở trạng thái hợp lệ).
    [Fact]
    public void AggregateRoots_AreCreatedThroughFactories()
    {
        Type[] roots = Domain.GetTypes().Where(t => t.IsSubclassOf(typeof(AggregateRoot)) && !t.IsAbstract).ToArray();

        Assert.Contains(typeof(Order), roots);
        Assert.All(roots, root => Assert.Empty(root.GetConstructors(BindingFlags.Public | BindingFlags.Instance)));
    }

    // Kiểm tra (b50): domain event và value object bất biến, sealed (không ai kế thừa để "lén" thêm field đổi được).
    [Fact]
    public void DomainEventsAndValueObjects_AreImmutable()
    {
        Type[] types = Domain.GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t.IsClass)
            .Concat([typeof(Money), typeof(PhoneNumber), typeof(OrderCode)])
            .ToArray();

        Assert.True(types.Length >= 6);
        Assert.All(types, t => Assert.True(t.IsSealed, $"{t.Name} phải sealed"));
        Assert.Empty(types.SelectMany(MutableProperties));
    }

    // Kiểm tra (b51): command/query là record bất biến (dữ liệu vào của use case không bị sửa giữa pipeline).
    [Fact]
    public void Requests_AreImmutable()
    {
        Type[] requests = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsRequestInterface))
            .ToArray();

        Assert.NotEmpty(requests);
        Assert.Empty(requests.SelectMany(MutableProperties));
    }

    // Kiểm tra (b51): mỗi command/query có ĐÚNG 1 handler — 0 thì lỗi lúc chạy, 2 thì không biết cái nào chạy.
    [Fact]
    public void EveryRequest_HasExactlyOneHandler()
    {
        Type[] handlerInterfaces = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .ToArray();

        string[] wrong = Application.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsRequestInterface))
            .Select(request => (request, count: handlerInterfaces.Count(h => h.GetGenericArguments()[0] == request)))
            .Where(x => x.count != 1)
            .Select(x => $"{x.request.Name}: {x.count} handler")
            .ToArray();

        Assert.Empty(wrong);
    }

    private static bool IsRequestInterface(Type i) => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>);
}
