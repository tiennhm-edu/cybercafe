// ============================================================================
// ServiceBoundaryTests.cs — RANH GIỚI giữa các service (Buổi 54–55 · microservice).
//
//   Order service (Api + Application + Infrastructure + Domain + Contracts)      Payment service (Payment.Api)
//                         \                                                       /
//                          └──────── CyberCafe.IntegrationEvents (hợp đồng) ─────┘
//                          └──────── CyberCafe.ServiceDefaults (hạ tầng chung) ──┘
//   Gateway: chỉ ServiceDefaults (biết URL, không biết code service nào).
//
// Cùng kỹ thuật với LayerDependencyTests (b48): đọc Assembly.GetReferencedAssemblies() — không thêm gói.
// Demo phá luật: cho Payment.Api tham chiếu CyberCafe.Domain để dùng enum PaymentMethod → test đỏ.
// 👉 Bước 8 (b54.md)
// ============================================================================
using System.Reflection;
using CyberCafe.Api.Controllers;
using CyberCafe.IntegrationEvents;
using CyberCafe.Payment.Api.Processing;
using Microsoft.Extensions.Hosting;

namespace CyberCafe.ArchitectureTests;

public class ServiceBoundaryTests
{
    private static readonly Assembly IntegrationEvents = typeof(IIntegrationEvent).Assembly;
    private static readonly Assembly ServiceDefaults = typeof(Extensions).Assembly;
    private static readonly Assembly PaymentService = typeof(PaymentRules).Assembly;
    private static readonly Assembly Gateway = typeof(GatewayPolicies).Assembly;

    // Mọi assembly thuộc Order service (Api là composition root — kéo theo các tầng bên trong)
    private static readonly Assembly[] OrderService =
    [
        typeof(ProductsController).Assembly,
        typeof(Application.DependencyInjection).Assembly,
        typeof(Infrastructure.DependencyInjection).Assembly,
        typeof(Domain.Orders.Order).Assembly,
        typeof(Contracts.Orders.OrderDto).Assembly,
    ];

    private static string[] CyberCafeReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith("CyberCafe.", StringComparison.Ordinal))
            .ToArray();

    // Kiểm tra: hợp đồng message chỉ dùng BCL — không kéo Domain, MassTransit, EF... sang service khác.
    [Fact]
    public void IntegrationEvents_ReferenceOnlyBaseClassLibrary()
    {
        string[] nonSystem = IntegrationEvents.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(nonSystem);
    }

    // Kiểm tra: Payment service KHÔNG tham chiếu bất kỳ phần nào của Order service (Domain, Contracts, Application,
    // Infrastructure, Api) — chỉ IntegrationEvents + ServiceDefaults.
    [Fact]
    public void PaymentService_ReferencesOnlySharedContracts()
    {
        Assert.Equal(
            ["CyberCafe.IntegrationEvents", "CyberCafe.ServiceDefaults"],
            CyberCafeReferences(PaymentService).Order().ToArray());
    }

    // Kiểm tra: 2 service chỉ "chạm" nhau ở đúng 2 project dùng chung (giao của tập tham chiếu).
    [Fact]
    public void OrderAndPaymentServices_ShareOnlyContractsAndDefaults()
    {
        HashSet<string> orderSide = OrderService.SelectMany(CyberCafeReferences).Concat(OrderService.Select(a => a.GetName().Name!)).ToHashSet();
        HashSet<string> paymentSide = [.. CyberCafeReferences(PaymentService), PaymentService.GetName().Name!];

        string[] shared = orderSide.Intersect(paymentSide).Order().ToArray();

        Assert.Equal(["CyberCafe.IntegrationEvents", "CyberCafe.ServiceDefaults"], shared);
    }

    // Kiểm tra: gateway và ServiceDefaults không biết code nghiệp vụ của service nào.
    [Fact]
    public void GatewayAndServiceDefaults_DoNotReferenceServiceCode()
    {
        Assert.Equal(["CyberCafe.ServiceDefaults"], CyberCafeReferences(Gateway));
        Assert.Empty(CyberCafeReferences(ServiceDefaults));
    }

    // Kiểm tra: Application vẫn sạch sau b55 — outbox/inbox chỉ là interface; MassTransit/RabbitMQ nằm ở Infrastructure.
    [Theory]
    [InlineData("MassTransit")]
    [InlineData("RabbitMQ")]
    [InlineData("Yarp")]
    [InlineData("OpenTelemetry")]
    public void Application_DoesNotReferenceMessagingOrHostingLibraries(string forbiddenPrefix)
    {
        string[] offending = typeof(Application.DependencyInjection).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(n => n.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offending);
    }

    // Kiểm tra: integration event là sealed record bất biến, chỉ chứa kiểu nguyên thủy (tuần tự hóa JSON an toàn,
    // không lộ kiểu Domain), và có EventId để bên nhận chống trùng.
    [Fact]
    public void IntegrationEvents_AreImmutablePrimitiveRecords()
    {
        Type[] allowed = [typeof(int), typeof(decimal), typeof(string), typeof(Guid), typeof(DateTime)];
        Type[] events = IntegrationEvents.GetTypes().Where(t => t is { IsClass: true } && typeof(IIntegrationEvent).IsAssignableFrom(t)).ToArray();

        Assert.True(events.Length >= 3);
        foreach (Type evt in events)
        {
            Assert.True(evt.IsSealed, $"{evt.Name} phải sealed");
            foreach (PropertyInfo property in evt.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != "EqualityContract"))
            {
                // init-only setter mang modreq IsExternalInit → không phải setter "thường"
                bool mutable = property.SetMethod is { IsPublic: true } setter
                    && !setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit");
                Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                Assert.False(mutable, $"{evt.Name}.{property.Name} có setter public");
                Assert.Contains(type, allowed);
            }
        }
    }
}
