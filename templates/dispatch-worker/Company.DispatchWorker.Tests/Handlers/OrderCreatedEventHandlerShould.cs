using Company.DispatchWorker.Handlers;
using Excalibur.Dispatch.Abstractions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Company.DispatchWorker.Tests.Handlers;

public class OrderCreatedEventHandlerShould
{
    [Fact]
    public async Task HandleOrderCreatedEventWithoutError()
    {
        // Arrange
        var logger = A.Fake<ILogger<OrderCreatedEventHandler>>();
        var context = A.Fake<IMessageContext>();
        var handler = new OrderCreatedEventHandler(logger);
        var @event = new OrderCreatedEvent(Guid.NewGuid(), "PROD-001", 3);

        // Act
        await handler.HandleAsync(@event, context, CancellationToken.None);

        // Assert — handler should complete without throwing
    }
}
