using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Testing;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Testing.ProductionCode;

namespace Testing;

/// <summary>
/// Demonstrates full-pipeline testing with <see cref="DispatchTestHarness"/>.
/// The harness wires up the real Dispatch pipeline so middleware, handler resolution,
/// and message tracking all run as they would in production.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PipelineIntegrationTests
{
    [Fact]
    public async Task Dispatched_command_returns_result_through_pipeline()
    {
        // Arrange
        var repo = A.Fake<IOrderRepository>();
        A.CallTo(() => repo.SaveAsync("Widget", 3, A<CancellationToken>._))
            .Returns("pipe-order-1");

        await using var harness = new DispatchTestHarness()
            .ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly))
            .ConfigureServices(s => s.AddSingleton(repo));

        var context = new MessageContextBuilder()
            .WithCorrelationId("test-corr-1")
            .Build();

        // Act
        var result = await harness.Dispatcher.DispatchAsync<CreateOrderCommand, string>(
            new CreateOrderCommand("Widget", 3), context, CancellationToken.None).ConfigureAwait(false);

        // Assert: pipeline produced a successful result
        result.Succeeded.ShouldBeTrue();
        result.ReturnValue.ShouldBe("pipe-order-1");
    }

    [Fact]
    public async Task Multiple_commands_produce_independent_results()
    {
        var repo = A.Fake<IOrderRepository>();
        A.CallTo(() => repo.SaveAsync("Gadget", 1, A<CancellationToken>._)).Returns("order-A");
        A.CallTo(() => repo.SaveAsync("Widget", 2, A<CancellationToken>._)).Returns("order-B");

        await using var harness = new DispatchTestHarness()
            .ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly))
            .ConfigureServices(s => s.AddSingleton(repo));

        // Act: dispatch two commands
        var ctxA = new MessageContextBuilder().Build();
        var resultA = await harness.Dispatcher.DispatchAsync<CreateOrderCommand, string>(
            new CreateOrderCommand("Gadget", 1), ctxA, CancellationToken.None).ConfigureAwait(false);

        var ctxB = new MessageContextBuilder().Build();
        var resultB = await harness.Dispatcher.DispatchAsync<CreateOrderCommand, string>(
            new CreateOrderCommand("Widget", 2), ctxB, CancellationToken.None).ConfigureAwait(false);

        // Assert: each dispatch produced its own result
        resultA.ReturnValue.ShouldBe("order-A");
        resultB.ReturnValue.ShouldBe("order-B");
    }

    [Fact]
    public async Task Event_dispatch_through_pipeline_succeeds()
    {
        await using var harness = new DispatchTestHarness()
            .ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(OrderCreatedConsumer).Assembly));

        var context = new MessageContextBuilder()
            .WithCorrelationId("event-corr-1")
            .Build();

        // Act: dispatch the event through the full pipeline
        var result = await harness.Dispatcher.DispatchAsync(
            new OrderCreatedEvent("order-99", "Doohickey"), context, CancellationToken.None).ConfigureAwait(false);

        // Assert: pipeline succeeded
        result.Succeeded.ShouldBeTrue();
    }
}
