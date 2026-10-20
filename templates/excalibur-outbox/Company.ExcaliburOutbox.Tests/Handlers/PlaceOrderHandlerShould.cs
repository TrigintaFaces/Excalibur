using Company.ExcaliburOutbox.Messages;
using Company.ExcaliburOutbox.Handlers;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Outbox;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Company.ExcaliburOutbox.Tests.Handlers;

public class PlaceOrderHandlerShould
{
    public static TheoryData<string?, int, decimal, string> InvalidOrders => new()
    {
        { null, 2, 1m, "ProductId" },
        { " ", 2, 1m, "ProductId" },
        { "P", 0, 1m, "Quantity" },
        { "P", -1, 1m, "Quantity" },
        { "P", 2, -1m, "UnitPrice" },
        { "P", int.MaxValue, decimal.MaxValue, "UnitPrice" },
    };

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public async Task RefuseInvalidOrdersBeforeBuffering(string? productId, int quantity, decimal price, string field)
    {
        var writer = A.Fake<IOutboxWriter>();
        var handler = new PlaceOrderHandler(writer, NullLogger<PlaceOrderHandler>.Instance);
        var exception = await Should.ThrowAsync<ValidationException>(() =>
            handler.HandleAsync(new PlaceOrderCommand(productId!, quantity, price), CancellationToken.None));
        exception.DispatchStatusCode.ShouldBe(400);
        exception.ValidationErrors.ShouldContainKey(field);
        A.CallTo(() => writer.WriteAsync(A<IDispatchMessage>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task BufferExactEventAndReturnItsIdentifier()
    {
        var writer = A.Fake<IOutboxWriter>();
        OrderPlacedEvent? buffered = null;
        A.CallTo(() => writer.WriteAsync(A<IDispatchMessage>._, null, A<CancellationToken>._))
            .Invokes(call => buffered = (OrderPlacedEvent)call.GetArgument<IDispatchMessage>(0)!);
        var handler = new PlaceOrderHandler(writer, NullLogger<PlaceOrderHandler>.Instance);
        var result = await handler.HandleAsync(new PlaceOrderCommand("PRODUCT", 3, 12.5m), CancellationToken.None);
        buffered.ShouldNotBeNull();
        result.ShouldNotBe(Guid.Empty);
        buffered.OrderId.ShouldBe(result);
        buffered.ProductId.ShouldBe("PRODUCT");
        buffered.Quantity.ShouldBe(3);
        buffered.TotalAmount.ShouldBe(37.5m);
        A.CallTo(() => writer.WriteAsync(A<IDispatchMessage>._, null, CancellationToken.None)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreserveWriterFailure()
    {
        var writer = A.Fake<IOutboxWriter>();
        var failure = new InvalidOperationException("STORAGE-REFUSAL");
        A.CallTo(() => writer.WriteAsync(A<IDispatchMessage>._, null, A<CancellationToken>._)).Throws(failure);
        var handler = new PlaceOrderHandler(writer, NullLogger<PlaceOrderHandler>.Instance);
        (await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new PlaceOrderCommand("PRODUCT", 1, 0m), CancellationToken.None))).ShouldBeSameAs(failure);
    }

    [Fact]
    public void CreateCommandWithCorrectProperties()
    {
        var command = new PlaceOrderCommand("PROD-001", 5, 12.5m);

        command.ProductId.ShouldBe("PROD-001");
        command.Quantity.ShouldBe(5);
        command.UnitPrice.ShouldBe(12.5m);
    }
}
