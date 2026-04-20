using Excalibur.Dispatch.Testing;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Testing.ProductionCode;

namespace Testing;

/// <summary>
/// Demonstrates unit testing handlers with <see cref="HandlerTestHarness{THandler}"/>
/// and FakeItEasy for dependency mocking.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HandlerUnitTests
{
    [Fact]
    public async Task Handler_saves_order_and_returns_id()
    {
        // Arrange: fake the repository so it returns a known ID
        var repo = A.Fake<IOrderRepository>();
        A.CallTo(() => repo.SaveAsync("Widget", 5, A<CancellationToken>._))
            .Returns("order-42");

        await using var harness = new HandlerTestHarness<CreateOrderHandler>()
            .ConfigureServices(s => s.AddSingleton(repo));

        // Act
        var orderId = await harness.HandleAsync<CreateOrderCommand, string>(
            new CreateOrderCommand("Widget", 5)).ConfigureAwait(false);

        // Assert
        orderId.ShouldBe("order-42");
        A.CallTo(() => repo.SaveAsync("Widget", 5, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handler_throws_on_zero_quantity()
    {
        var repo = A.Fake<IOrderRepository>();

        await using var harness = new HandlerTestHarness<CreateOrderHandler>()
            .ConfigureServices(s => s.AddSingleton(repo));

        // Act & Assert: invalid quantity triggers ArgumentException
        await Should.ThrowAsync<ArgumentException>(
            () => harness.HandleAsync<CreateOrderCommand, string>(
                new CreateOrderCommand("Widget", 0))).ConfigureAwait(false);

        A.CallTo(repo).MustNotHaveHappened();
    }

    [Fact]
    public async Task Handler_can_be_resolved_directly()
    {
        var repo = A.Fake<IOrderRepository>();
        A.CallTo(() => repo.SaveAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns("direct-id");

        await using var harness = new HandlerTestHarness<CreateOrderHandler>()
            .ConfigureServices(s => s.AddSingleton(repo));

        // Access the handler instance directly for fine-grained testing
        var handler = harness.Handler;
        handler.ShouldNotBeNull();

        var result = await handler.HandleAsync(
            new CreateOrderCommand("Gadget", 1), CancellationToken.None).ConfigureAwait(false);

        result.ShouldBe("direct-id");
    }
}
