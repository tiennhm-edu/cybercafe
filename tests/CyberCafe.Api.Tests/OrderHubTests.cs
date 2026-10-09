// ============================================================================
// OrderHubTests.cs — test SignalR THẬT qua TestServer (Buổi 33–34).
// HubConnection (.NET client — giống Web dùng) kết nối tới hub chạy trong WebApplicationFactory.
// Transport LongPolling: đi qua HttpMessageHandler của TestServer (không cần cổng mạng thật).
// ============================================================================
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CyberCafe.Contracts.Orders;
using CyberCafe.Contracts.Realtime;
using CyberCafe.Domain.Orders;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
// AddJsonProtocol của HubConnectionBuilder nằm trong namespace DependencyInjection
using Microsoft.Extensions.DependencyInjection;

namespace CyberCafe.Api.Tests;

public class OrderHubTests
{
    private static HubConnection Connect(TestServer server) => new HubConnectionBuilder()
        .WithUrl(new Uri(server.BaseAddress, OrderHubContract.Path.TrimStart('/')), o =>
        {
            o.Transports = HttpTransportType.LongPolling;
            o.HttpMessageHandlerFactory = _ => server.CreateHandler(); // gửi qua TestServer thay vì mạng
        })
        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
        .Build();

    // Kiểm tra: barista trong group nhận OrderPlaced khi khách đặt hàng; khách theo dõi đơn nhận OrderStatusChanged.
    [Fact]
    public async Task Baristas_ReceiveOrderPlaced_And_Watcher_ReceivesStatusChanged()
    {
        await using CyberCafeApiFactory factory = new() { UseRealNotifier = true };
        HttpClient client = factory.CreateClient(); // khởi động TestServer

        await using HubConnection barista = Connect(factory.Server);
        await using HubConnection customer = Connect(factory.Server);

        // TaskCompletionSource: "hứa" sẽ có kết quả khi sự kiện tới → test await được với timeout
        TaskCompletionSource<OrderDto> placed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<OrderDto> changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        barista.On<OrderDto>(OrderHubContract.OrderPlaced, o => placed.TrySetResult(o));
        customer.On<OrderDto>(OrderHubContract.OrderStatusChanged, o => changed.TrySetResult(o));

        await barista.StartAsync();
        await barista.InvokeAsync(OrderHubContract.JoinBaristas);

        OrderDto order = await client.PlaceAsync();
        OrderDto received = await placed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(order.Id, received.Id);

        await customer.StartAsync();
        await customer.InvokeAsync(OrderHubContract.WatchOrder, order.Id);
        await client.PutAsync($"/api/orders/{order.Id}/status",
            JsonContent.Create(new { status = "Preparing" }));

        Assert.Equal(OrderStatus.Preparing, (await changed.Task.WaitAsync(TimeSpan.FromSeconds(10))).Status);
    }
}
