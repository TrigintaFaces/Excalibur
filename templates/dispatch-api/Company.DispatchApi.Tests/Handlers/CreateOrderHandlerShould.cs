using Company.DispatchApi.Actions;
using Company.DispatchApi.Handlers;
using Company.DispatchApi.Infrastructure;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace Company.DispatchApi.Tests.Handlers;

public class CreateOrderHandlerShould
{
    [Fact]
    public async Task ReturnNewOrderId()
    {
        // Arrange
        var orderStore = new InMemoryOrderStore();
        var logger = A.Fake<ILogger<CreateOrderHandler>>();
        var handler = new CreateOrderHandler(orderStore, logger);
        var action = new CreateOrderAction("PROD-001", 5);

        // Act
        var orderId = await handler.HandleAsync(action, CancellationToken.None);

        // Assert
        orderId.ShouldNotBe(Guid.Empty);
    }
}
