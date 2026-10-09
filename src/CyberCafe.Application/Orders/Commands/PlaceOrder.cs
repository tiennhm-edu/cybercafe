// ============================================================================
// PlaceOrder.cs — use case ĐẶT HÀNG theo CQRS (Buổi 51 · command; Buổi 52 · validator; Buổi 49–50 · aggregate;
//                Buổi 55 · outbox — thanh toán bất đồng bộ qua Payment service).
// 1 file = 1 use case trọn vẹn ("vertical slice"): command (dữ liệu vào) + validator + handler.
// So với b48 OrderService.PlaceAsync:
//   - Không còn Cart.ToOrder + Checkout: dựng thẳng aggregate Order.Create → AddItem → ApplyDiscount → Pay.
//   - Không gọi notifier: Pay() phát OrderPlaced; handler NotifyBaristasOnOrderPlaced lo báo quầy SAU khi lưu.
//   - Validate hình dạng dữ liệu ở PlaceOrderCommandValidator (chạy trong ValidationBehavior, trước handler).
// Buổi 55: 2 nhánh theo OrderingSettings.PaymentFlow:
//   InProcess  — giữ nguyên b53 (Pay ngay, 1 lần SaveChanges).
//   Messaging  — KHÔNG Pay. Lưu đơn (lần 1, để có Id) → ghi OrderPlacedIntegrationEvent vào outbox → lưu (lần 2).
//                Cả 2 lần nằm trong 1 transaction của TransactionBehavior (b52) → đơn và message cùng commit.
// ============================================================================
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Common.Interfaces;
using CyberCafe.Application.Common.Messaging;
using CyberCafe.Application.Products;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Discounts;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.People;
using CyberCafe.Domain.Products;
using CyberCafe.IntegrationEvents;
using FluentValidation;
using ValidationException = CyberCafe.Application.Common.Exceptions.ValidationException;

namespace CyberCafe.Application.Orders.Commands;

// 👉 Bước 4 (b51.md)
/// <summary>Đặt hàng. Record bất biến: command là "ảnh chụp" yêu cầu, không ai sửa giữa đường.</summary>
/// <param name="UserId">Chủ đơn — lấy từ TOKEN ở Api, không bao giờ từ body.</param>
/// <param name="CustomerName">Họ tên khách.</param>
/// <param name="PhoneNumber">SĐT khách.</param>
/// <param name="Items">Các dòng món (chỉ Id, size, số lượng — KHÔNG có giá).</param>
/// <param name="DiscountCode">Mã giảm giá (server tự tra).</param>
/// <param name="PaymentMethod">Hình thức thanh toán.</param>
/// <param name="CardNumber">Số thẻ (chỉ khi Card; chỉ lưu 4 số cuối).</param>
/// <param name="Note">Ghi chú.</param>
public sealed record PlaceOrderCommand(
    int? UserId,
    string CustomerName,
    string PhoneNumber,
    IReadOnlyList<OrderLineRequest> Items,
    string? DiscountCode,
    PaymentMethod PaymentMethod,
    string? CardNumber,
    string? Note) : ICommand<OrderDto>
{
    /// <summary>Dựng command từ body HTTP (Contracts) + id người dùng trong token.</summary>
    public static PlaceOrderCommand From(PlaceOrderRequest request, int? userId) => new(
        userId, request.CustomerName, request.PhoneNumber, request.Items, request.DiscountCode,
        request.PaymentMethod, request.CardNumber, request.Note);
}

// 👉 Bước 3 (b52.md)
/// <summary>Luật HÌNH DẠNG dữ liệu (giống DataAnnotations của PlaceOrderRequest — nhưng chạy cả khi không qua HTTP).</summary>
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    /// <summary>Khai báo luật bằng fluent API: RuleFor(field).Điều_kiện().WithMessage(...).</summary>
    public PlaceOrderCommandValidator()
    {
        this.RuleFor(c => c.CustomerName).NotEmpty().WithMessage("Vui lòng nhập họ tên")
            .Length(2, 50).WithMessage("Họ tên từ 2 đến 50 ký tự");
        // Luật SĐT lấy từ value object → 1 nguồn sự thật cho Domain, validator và (gián tiếp) Web
        this.RuleFor(c => c.PhoneNumber).Must(PhoneNumber.IsValid)
            .WithMessage("Số điện thoại gồm 10–11 chữ số, bắt đầu bằng 0");
        this.RuleFor(c => c.Items).NotEmpty().WithMessage("Đơn hàng phải có ít nhất 1 món");
        // RuleForEach: áp luật cho TỪNG phần tử; tên lỗi tự thành "Items[0].Quantity"
        this.RuleForEach(c => c.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0).WithMessage("Mã món không hợp lệ");
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 20).WithMessage("Số lượng từ 1 đến 20");
            line.RuleFor(l => l.Size).IsInEnum();
        });
        this.RuleFor(c => c.DiscountCode).MaximumLength(20);
        this.RuleFor(c => c.Note).MaximumLength(200).WithMessage("Ghi chú tối đa 200 ký tự");
        this.RuleFor(c => c.PaymentMethod).IsInEnum();
        // When(...): luật CÓ ĐIỀU KIỆN — chỉ kiểm tra số thẻ khi chọn thanh toán thẻ
        this.RuleFor(c => c.CardNumber)
            .Must(card => card is { Length: >= 12 } && card.All(char.IsDigit))
            .When(c => c.PaymentMethod == PaymentMethod.Card)
            .WithMessage("Số thẻ gồm ít nhất 12 chữ số");
    }
}

/// <summary>Tra món + mã giảm giá, dựng aggregate, lưu. Domain event lo phần báo realtime.</summary>
/// <remarks>
/// Buổi 55: <paramref name="settings"/> và <paramref name="outbox"/> là tham số TÙY CHỌN — DI luôn truyền (đã đăng ký),
/// còn unit test b53 tạo handler bằng 3 tham số vẫn chạy y như cũ (mặc định InProcess).
/// </remarks>
public sealed class PlaceOrderCommandHandler(
    IProductRepository products,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    OrderingSettings? settings = null,
    IIntegrationEventOutbox? outbox = null) : ICommandHandler<PlaceOrderCommand, OrderDto>
{
    /// <inheritdoc />
    public async Task<OrderDto> Handle(PlaceOrderCommand command, CancellationToken ct)
    {
        // 1) Lấy các món trong 1 câu SQL (WHERE Id IN ...), bản TRACKED: Order tham chiếu các Product này;
        //    không tracked thì EF tưởng là món MỚI và INSERT lại → trùng khóa.
        int[] ids = command.Items.Select(i => i.ProductId).Distinct().ToArray();
        IReadOnlyDictionary<int, Product> found = await products.GetByIdsAsync(ids, ct);

        int[] missing = ids.Where(id => !found.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw ValidationException.For(nameof(command.Items), $"Không có món với mã: {string.Join(", ", missing)}");
        }

        // 2) Mã giảm giá: client chỉ gửi MÃ, server tự tra (DiscountCatalog dùng chung với Web).
        Discount? discount = null;
        if (!string.IsNullOrWhiteSpace(command.DiscountCode))
        {
            discount = DiscountCatalog.FindByCode(command.DiscountCode)
                ?? throw ValidationException.For(nameof(command.DiscountCode), "Mã giảm giá không hợp lệ");
        }

        // 3) 👉 Bước 2 (b49.md): dựng aggregate qua method nghiệp vụ — mỗi bước aggregate tự kiểm tra invariant
        //    (món tạm hết → DomainException → 409). PhoneNumber.Create: SĐT sai → ArgumentException → 400.
        PhoneNumber phone = PhoneNumber.Create(command.PhoneNumber);
        Order order = Order.Create(new Customer(command.CustomerName, phone), command.Note, command.UserId);
        foreach (OrderLineRequest line in command.Items)
        {
            order.AddItem(found[line.ProductId], line.Size, line.Quantity);
        }

        if (discount is not null)
        {
            order.ApplyDiscount(discount);
        }

        if ((settings ?? OrderingSettings.Default).PaymentFlow == PaymentFlow.Messaging)
        {
            return await this.PlaceAndRequestPaymentAsync(order, command, ct);
        }

        // Pay: chốt số tiền, Process() đa hình, cộng điểm, Raise(OrderPaid + OrderPlaced)
        order.Pay(PaymentFactory.Create(command.PaymentMethod, order.FinalAmount.Amount, phone.Value, command.CardNumber));

        // 4) Lưu (INSERT Payments, OrderDiscounts, Orders, OrderItems). Domain event được phát SAU khi lưu/commit
        //    → NotifyBaristasOnOrderPlaced gửi SignalR. Handler này không biết gì về SignalR.
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(ct);
        return order.ToDto();
    }

    // 👉 Bước 2 (b55.md): nhánh microservice — lưu đơn + message trong CÙNG 1 transaction (Outbox pattern)
    private async Task<OrderDto> PlaceAndRequestPaymentAsync(Order order, PlaceOrderCommand command, CancellationToken ct)
    {
        if (outbox is null)
        {
            throw new InvalidOperationException("PaymentFlow = Messaging nhưng chưa đăng ký IIntegrationEventOutbox (AddInfrastructure).");
        }

        // Lần lưu 1: INSERT Orders + OrderItems → SQL Server sinh Id (IDENTITY). Message cần OrderId nên phải lưu trước.
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(ct);

        // Lần lưu 2: 1 dòng OutboxMessages. Nếu dòng này lỗi → TransactionBehavior rollback CẢ đơn ở lần lưu 1.
        // ⚠️ Lỗi hay gặp: gọi thẳng bus.Publish(...) ở đây → message đi trước khi commit; commit lỗi = Payment thu tiền đơn không tồn tại.
        // ⚠️ Lỗi hay gặp: gửi command.CardNumber (đủ 16 số) vào event — chỉ gửi 4 số cuối.
        string? cardLast4 = command.PaymentMethod == PaymentMethod.Card ? command.CardNumber?[^4..] : null;
        outbox.Enqueue(new OrderPlacedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, order.Id, order.Code.Value, order.FinalAmount.Amount,
            command.PaymentMethod.ToString(), cardLast4));
        await unitOfWork.SaveChangesAsync(ct);

        // Trả 201 ngay với đơn Pending, chưa thanh toán (PaymentMethod = null). Khách thấy "Đang xử lý thanh toán";
        // khi Payment trả lời, ConfirmOrderPaymentCommand gọi order.Pay → OrderPlaced → barista + khách nhận SignalR.
        return order.ToDto();
    }
}
