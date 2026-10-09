// ============================================================================
// PagedResult.cs — 1 "trang" dữ liệu + thông tin phân trang (Buổi 32–41 · LINQ Skip/Take).
// Vì sao không trả thẳng List<T>? Menu có thể có hàng trăm món: client chỉ cần 1 trang
// và cần biết TỔNG số bản ghi để vẽ nút "Trang sau".
// ============================================================================
namespace CyberCafe.Contracts.Common;

/// <summary>Kết quả phân trang dùng chung cho mọi API trả danh sách.</summary>
/// <typeparam name="T">Kiểu phần tử (vd ProductDto).</typeparam>
/// <param name="Items">Các phần tử của trang hiện tại.</param>
/// <param name="Page">Số trang (bắt đầu từ 1).</param>
/// <param name="PageSize">Số phần tử tối đa mỗi trang.</param>
/// <param name="TotalCount">Tổng số phần tử thỏa điều kiện lọc (mọi trang).</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Tổng số trang, làm tròn lên: 8 món, 3 món/trang → 3 trang.</summary>
    public int TotalPages => this.PageSize <= 0 ? 0 : (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);

    /// <summary>Còn trang trước không.</summary>
    public bool HasPrevious => this.Page > 1;

    /// <summary>Còn trang sau không.</summary>
    public bool HasNext => this.Page < this.TotalPages;
}
