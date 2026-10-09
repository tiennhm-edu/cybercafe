// ============================================================================
// MenuServiceTests.cs — unit test cho MenuService (Buổi 23 · xUnit).
// Vì MenuService là class C# thường (không phụ thuộc Blazor), ta new() trực tiếp
// để test mà không cần chạy web. Chạy: dotnet test
// ============================================================================
using CyberCafe.Web.Services;

namespace CyberCafe.Tests;

public class MenuServiceTests
{
    // xUnit tạo 1 instance MỚI của class test cho MỖI test → _service luôn "sạch", các test không ảnh hưởng nhau.
    private readonly MenuService _service = new();

    // [Fact]: 1 test không tham số. Đặt tên theo mẫu Method_TìnhHuống_KếtQuảMongĐợi.
    // Kiểm tra: dữ liệu seed không rỗng.
    [Fact]
    public void GetAll_ReturnsSeededItems()
    {
        Assert.NotEmpty(_service.GetAll());
    }

    // Kiểm tra: có đủ 3 nhóm Cà phê / Trà / Bánh.
    [Fact]
    public void GetCategories_ContainsCoffeeTeaAndCake()
    {
        var categories = _service.GetCategories();

        Assert.Contains("Cà phê", categories);
        Assert.Contains("Trà", categories);
        Assert.Contains("Bánh", categories);
    }

    // [Theory] + [InlineData]: cùng 1 test chạy lại với nhiều bộ dữ liệu khác nhau.
    // Kiểm tra: lọc theo nhóm chỉ trả về món thuộc đúng nhóm đó.
    [Theory]
    [InlineData("Cà phê")]
    [InlineData("Bánh")]
    public void GetByCategory_ReturnsOnlyThatCategory(string category)
    {
        var items = _service.GetByCategory(category);

        Assert.NotEmpty(items);
        Assert.All(items, x => Assert.Equal(category, x.Category));
    }

    // Kiểm tra: Id không tồn tại → null (không ném exception).
    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(_service.GetById(9999));
    }

    // Kiểm tra: mọi món đều có giá > 0.
    [Fact]
    public void AllPrices_ArePositive()
    {
        Assert.All(_service.GetAll(), x => Assert.True(x.Price > 0));
    }
}
