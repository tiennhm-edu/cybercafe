// ============================================================================
// OrderHandlerTests.cs — unit test command/query/event handler của đơn hàng (Buổi 50–51).
// Gọi THẲNG handler.Handle(...) với port giả — không dispatcher, không DI, không database.
// Test cả pipeline (validation/transaction/logging) nằm ở PipelineTests.cs.
// ============================================================================
using CyberCafe.Application.Common;
using CyberCafe.Application.Common.Exceptions;
using CyberCafe.Application.Orders.Commands;
using CyberCafe.Application.Orders.EventHandlers;
using CyberCafe.Application.Orders.Queries;
using CyberCafe.Contracts.Orders;
using CyberCafe.Domain.Common;
using CyberCafe.Domain.Orders;
using CyberCafe.Domain.Orders.Events;
using CyberCafe.Domain.Payments;
using CyberCafe.Domain.Products;

namespace CyberCafe.Application.Tests;

public class OrderHandlerTests
{
    private static readonly CurrentUser Barista = new(UserId: 2, IsStaff: true);
    private static readonly CurrentUser Alice = new(UserId: 3, IsStaff: false);
    private static readonly CurrentUser Bob = new(UserId: 4, IsStaff: false);

    private static PlaceOrderCommand Command(string? discount = null, params OrderLineRequest[] lines) => new(
        UserId: 3,
        CustomerName: "Nguyễn Văn An",
        PhoneNumber: "0901234567",
        Items: lines.Length > 0 ? lines : [new OrderLineRequest(1, DrinkSize.M, 2), new OrderLineRequest(7, DrinkSize.L, 1)],
        DiscountCode: discount,
        PaymentMethod: PaymentMethod.Cash,
        CardNumber: null,
        Note: "Ít đá");

    // Kiểm tra: đặt hàng hợp lệ → tính tiền theo size + voucher, gắn chủ đơn, lưu đúng 1 lần, aggregate có OrderPaid + OrderPlaced.
    [Fact]
    public async Task PlaceOrder_Valid_BuildsPaidAggregate_AndSavesOnce()
    {
        FakeOrderRepository orders = new();
        FakeUnitOfWork uow = new();
        PlaceOrderCommandHandler handler = new(new FakeProductRepository(Sample.CaPheSua(), Sample.Tiramisu()), orders, uow);

        OrderDto dto = await handler.Handle(Command("GIAM20K"), CancellationToken.None);

        Order order = Assert.Single(orders.Added);
        Assert.Equal((103000m, 20000m, 83000m), (dto.TotalAmount, dto.DiscountAmount, dto.FinalAmount));
        Assert.Equal(3, order.OwnerId);
        Assert.True(order.IsPaid);
        Assert.Equal(1, uow.Saves);
        // Event chỉ được GHI LẠI trên aggregate; phát là việc của DbContext sau SaveChanges (fake không phát)
        Assert.Single(order.DomainEvents.OfType<OrderPaid>());
        Assert.Single(order.DomainEvents.OfType<OrderPlaced>());
    }

    // Kiểm tra: món không tồn tại / mã giảm giá sai → ValidationException theo đúng field, KHÔNG lưu gì.
    [Theory]
    [InlineData("unknown-product", "Items")]
    [InlineData("bad-code", "DiscountCode")]
    public async Task PlaceOrder_InvalidReferences_ThrowValidation(string scenario, string field)
    {
        FakeUnitOfWork uow = new();
        PlaceOrderCommandHandler handler = new(new FakeProductRepository(Sample.CaPheSua(), Sample.Tiramisu()), new FakeOrderRepository(), uow);
        PlaceOrderCommand command = scenario == "bad-code" ? Command("FREE100") : Command(null, new OrderLineRequest(999, DrinkSize.S, 1));

        ValidationException ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Contains(field, ex.Errors.Keys);
        Assert.Equal(0, uow.Saves);
    }

    // Kiểm tra: món tạm hết → aggregate từ chối (DomainException → 409), không lưu.
    [Fact]
    public async Task PlaceOrder_UnavailableProduct_DomainRejects()
    {
        FakeUnitOfWork uow = new();
        PlaceOrderCommandHandler handler = new(new FakeProductRepository(Sample.Matcha()), new FakeOrderRepository(), uow);

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(Command(null, new OrderLineRequest(6, DrinkSize.S, 1)), CancellationToken.None));
        Assert.Equal(0, uow.Saves);
    }

    // Kiểm tra: barista đổi trạng thái hợp lệ → lưu + aggregate phát OrderStatusChanged; nhảy cóc → DomainException, không lưu.
    [Fact]
    public async Task ChangeStatus_ValidThenSkipping()
    {
        Order order = Sample.PaidOrder(10, ownerId: 3);
        FakeUnitOfWork uow = new();
        ChangeOrderStatusCommandHandler handler = new(new FakeOrderRepository(order), uow);

        OrderDto dto = await handler.Handle(new ChangeOrderStatusCommand(10, OrderStatus.Preparing, Barista), CancellationToken.None);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new ChangeOrderStatusCommand(10, OrderStatus.Completed, Barista), CancellationToken.None));

        Assert.Equal(OrderStatus.Preparing, dto.Status);
        Assert.Equal(1, uow.Saves);
        Assert.Single(order.DomainEvents.OfType<OrderStatusChanged>());
    }

    // Kiểm tra (IDOR): đơn không tồn tại HOẶC của người khác → NotFoundException như nhau.
    [Fact]
    public async Task ChangeStatus_UnknownOrOthersOrder_NotFound()
    {
        ChangeOrderStatusCommandHandler handler = new(new FakeOrderRepository(Sample.PaidOrder(10, ownerId: 3)), new FakeUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ChangeOrderStatusCommand(99, OrderStatus.Preparing, Barista), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ChangeOrderStatusCommand(10, OrderStatus.Preparing, Bob), CancellationToken.None));
    }

    // Kiểm tra: khách hủy đơn của mình khi Pending được; khi đang pha → DomainException; khách khác → 404.
    [Fact]
    public async Task CancelOrder_CustomerRules()
    {
        Order pending = Sample.PaidOrder(10, ownerId: 3);
        Order preparing = Sample.PaidOrder(11, ownerId: 3);
        preparing.StartPreparing();
        CancelOrderCommandHandler handler = new(new FakeOrderRepository(pending, preparing), new FakeUnitOfWork());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CancelOrderCommand(10, Bob), CancellationToken.None));
        OrderDto cancelled = await handler.Handle(new CancelOrderCommand(10, Alice), CancellationToken.None);
        DomainException ex = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new CancelOrderCommand(11, Alice), CancellationToken.None));
        OrderDto byStaff = await handler.Handle(new CancelOrderCommand(11, Barista), CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Contains("liên hệ quầy", ex.Message);
        Assert.Equal(OrderStatus.Cancelled, byStaff.Status);
    }

    // Kiểm tra: bảng quầy không truyền status → mặc định 3 trạng thái đang chạy; page/pageSize bị kẹp vào khoảng an toàn.
    [Fact]
    public async Task BaristaBoard_DefaultsActiveStatuses_AndClampsPaging()
    {
        FakeOrderReadStore store = new();
        OrderQueryHandlers handlers = new(store);

        await handlers.Handle(new GetBaristaBoardQuery(null, Page: 0, PageSize: 1000), CancellationToken.None);

        Assert.Equal([OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready], store.LastBoard!.Value.Statuses);
        Assert.Equal((1, 100), (store.LastBoard.Value.Page, store.LastBoard.Value.PageSize));
    }

    // Kiểm tra: handler domain event chuyển aggregate thành DTO và gọi đúng method của notifier.
    [Fact]
    public async Task EventHandlers_NotifyWithDto()
    {
        Order order = Sample.PaidOrder(12, ownerId: 3);
        RecordingNotifier notifier = new();

        await new NotifyBaristasOnOrderPlaced(notifier).Handle(new OrderPlaced(order, DateTime.Now), CancellationToken.None);
        await new NotifyOnOrderStatusChanged(notifier).Handle(new OrderStatusChanged(order, OrderStatus.Pending, OrderStatus.Preparing, DateTime.Now), CancellationToken.None);

        Assert.Equal("CC-0012", Assert.Single(notifier.Placed).Code);
        Assert.Equal(12, Assert.Single(notifier.StatusChanged).Id);
    }
}
