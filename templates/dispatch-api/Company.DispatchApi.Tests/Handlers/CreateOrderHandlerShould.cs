using Company.DispatchApi.Actions;
using Company.DispatchApi.Handlers;
using Excalibur.Dispatch.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Company.DispatchApi.Tests.Handlers;

public class CreateOrderHandlerShould
{
    [Fact]
    public async Task SetResultWithNewOrderId()
    {
        // Arrange
        var logger = A.Fake<ILogger<CreateOrderHandler>>();
        var context = A.Fake<IMessageContext>();
        var handler = new CreateOrderHandler(logger);
        var action = new CreateOrderAction("PROD-001", 5);

        // Act
        await handler.HandleAsync(action, context, CancellationToken.None);

        // Assert
        A.CallTo(() => context.SetResult(A<Guid>.That.Not.IsEqualTo(Guid.Empty)))
            .MustHaveHappenedOnceExactly();
    }
}
