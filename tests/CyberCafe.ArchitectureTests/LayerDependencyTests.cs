// ============================================================================
// LayerDependencyTests.cs — kiểm tra LUẬT PHỤ THUỘC của Clean Architecture (Buổi 48).
//
//          Api (controller, hub, Program.cs = composition root)
//           │                    │
//           ▼                    ▼
//      Application ◄──── Infrastructure (EF Core, Redis, JWT, BCrypt, SignalR adapter)
//           │
//           ▼
//        Domain (C# thuần)          Contracts (DTO dùng chung Api ↔ Web) → Domain
//
// Cách kiểm tra: Assembly.GetReferencedAssemblies() = danh sách assembly mà code THỰC SỰ dùng
// (trình biên dịch chỉ ghi assembly có kiểu được dùng trong IL) → đúng thứ ta muốn cấm.
// 👉 Bước 11 (b48.md)
// ============================================================================
using System.Reflection;
using CyberCafe.Api.Controllers;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Orders;
using Microsoft.AspNetCore.Mvc;

namespace CyberCafe.ArchitectureTests;

public class LayerDependencyTests
{
    // Lấy assembly qua 1 kiểu "đại diện" của mỗi tầng — đổi tên/di chuyển kiểu đó thì test báo lỗi biên dịch ngay.
    private static readonly Assembly Domain = typeof(Order).Assembly;
    private static readonly Assembly Contracts = typeof(OrderDto).Assembly;
    private static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(ProductsController).Assembly;

    private static readonly Dictionary<string, Assembly> Layers = new()
    {
        ["Domain"] = Domain,
        ["Contracts"] = Contracts,
        ["Application"] = Application,
        ["Infrastructure"] = Infrastructure,
        ["Api"] = Api,
    };

    // ⚠️ Lỗi hay gặp: kiểm tra bằng file .csproj (PackageReference) — gói tham chiếu nhưng không dùng vẫn "qua",
    //    còn gói kéo theo gián tiếp (transitive) lại lọt. Đọc metadata của assembly đã biên dịch là chắc nhất.
    private static IEnumerable<string> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    // Kiểm tra: Domain chỉ dùng thư viện chuẩn .NET (System.*) — không tầng nào khác, không gói NuGet nào.
    [Fact]
    public void Domain_ReferencesOnlyBaseClassLibrary()
    {
        // System.Runtime, System.Linq, System.Collections... là BCL; thứ khác (Microsoft.*, CyberCafe.*) đều sai
        string[] nonSystem = ReferencedNames(Domain).Where(n => !n.StartsWith("System", StringComparison.Ordinal)).ToArray();

        Assert.Empty(nonSystem);
    }

    // Kiểm tra: tầng TRONG (Domain, Contracts, Application) không biết EF Core, ASP.NET Core, Redis, BCrypt
    // và không "nhìn ra" tầng ngoài (Infrastructure, Api). Đây là luật quan trọng nhất của Clean Architecture.
    [Theory]
    [InlineData("Domain", "Microsoft.EntityFrameworkCore")]
    [InlineData("Domain", "Microsoft.AspNetCore")]
    [InlineData("Domain", "CyberCafe.Application")]
    [InlineData("Domain", "CyberCafe.Infrastructure")]
    [InlineData("Contracts", "Microsoft.EntityFrameworkCore")]
    [InlineData("Contracts", "Microsoft.AspNetCore")]
    [InlineData("Contracts", "CyberCafe.Application")]
    [InlineData("Application", "Microsoft.EntityFrameworkCore")]
    [InlineData("Application", "Microsoft.AspNetCore")]
    [InlineData("Application", "Microsoft.Extensions.Caching")]
    [InlineData("Application", "StackExchange.Redis")]
    [InlineData("Application", "BCrypt")]
    [InlineData("Application", "Microsoft.IdentityModel")]
    [InlineData("Application", "CyberCafe.Infrastructure")]
    [InlineData("Application", "CyberCafe.Api")]
    [InlineData("Infrastructure", "CyberCafe.Api")]
    public void InnerLayer_DoesNotReference(string layer, string forbiddenPrefix)
    {
        string[] offending = ReferencedNames(Layers[layer])
            .Where(n => n.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.True(offending.Length == 0, $"{layer} không được tham chiếu {forbiddenPrefix}: {string.Join(", ", offending)}");
    }

    // Kiểm tra: Domain "sạch" cả ở mức attribute — không [Table], [Key], [Column]... (cấu hình map nằm ở
    // Infrastructure/Persistence/Configurations bằng Fluent API).
    [Fact]
    public void DomainTypes_HaveNoPersistenceAttributes()
    {
        // Duyệt cả kiểu lẫn property (kể cả private) — [Key] gắn trên property private vẫn là "dính" EF
        IEnumerable<MemberInfo> members = Domain.GetTypes()
            .SelectMany(t => new MemberInfo[] { t }.Concat(t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)));

        string[] offending = members
            .SelectMany(m => m.GetCustomAttributes(inherit: false).Select(a => (Member: m, Attr: a.GetType())))
            .Where(x => x.Attr.Namespace is { } ns
                && (ns.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || ns.StartsWith("System.ComponentModel.DataAnnotations", StringComparison.Ordinal)))
            .Select(x => $"{x.Member.DeclaringType?.Name}.{x.Member.Name} [{x.Attr.Name}]")
            .ToArray();

        Assert.Empty(offending);
    }

    // Kiểm tra: controller MỎNG — constructor chỉ nhận kiểu của Application (use case / port),
    // không nhận thẳng DbContext, MenuCache... của Infrastructure.
    [Fact]
    public void Controllers_DoNotDependOnInfrastructureTypes()
    {
        string[] offending = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => (Controller: t, p.ParameterType)))
            .Where(x => x.ParameterType.Assembly == Infrastructure)
            .Select(x => $"{x.Controller.Name}({x.ParameterType.Name})")
            .ToArray();

        Assert.Empty(offending);
    }

    // Kiểm tra: mọi interface ("port") Application khai báo mà KHÔNG tự cài đặt thì phải có "adapter"
    // ở Infrastructure hoặc Api — tránh quên đăng ký và lỗi DI lúc chạy.
    [Fact]
    public void EveryApplicationPort_HasAnImplementation()
    {
        // Ứng viên cài đặt: class cụ thể ở 3 tầng (Application tự cài các interface nội bộ, Infrastructure/Api cài port)
        Type[] candidates = [.. Application.GetTypes(), .. Infrastructure.GetTypes(), .. Api.GetTypes()];
        candidates = candidates.Where(t => t is { IsClass: true, IsAbstract: false }).ToArray();

        string[] missing = Application.GetTypes()
            .Where(t => t is { IsInterface: true, IsPublic: true })
            .Where(port => !candidates.Any(c => Implements(c, port)))
            .Select(port => port.Name)
            .ToArray();

        Assert.Empty(missing);
    }

    // Kiểu c có implement interface port không (port generic như IFoo<> thì so theo định nghĩa generic)
    private static bool Implements(Type c, Type port) => c.GetInterfaces().Any(i =>
        i == port || (port.IsGenericTypeDefinition && i.IsGenericType && i.GetGenericTypeDefinition() == port));
}
