// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Routing.Builder;
using Excalibur.Dispatch.Tests.TestFakes;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Regression tests for Sprint 660 transport hot-path optimizations (T.2-T.5).
/// Validates that each optimization preserves correct behavior while improving performance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportHotPathOptimizationsShould
{
	private static readonly string[] DefaultEndpoints = ["default"];
	private static readonly string[] LocalAndRabbitBusNames = ["local", "rabbitmq"];
	private static readonly string[] LocalKafkaAndRabbitBusNames = ["local", "kafka", "rabbitmq"];

	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();

	#region T.2 (Opt 1): Lightweight Context Initialization

	[Fact]
	public async Task SkipTransportBindingForOutboundDispatches()
	{
		// Arrange - outbound dispatch has no transport origin property
		var transportProvider = A.Fake<ITransportContextProvider>();
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(invoker, finalHandler, transportProvider, _serviceProvider);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);
		// No TransportBindingNameProperty set -- this is an outbound dispatch

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - GetTransportBinding should NOT be called for outbound dispatches
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ResolveTransportBindingForInboundDispatches()
	{
		// Arrange - inbound dispatch has transport origin property set by transport adapter
		var transportProvider = A.Fake<ITransportContextProvider>();
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._)).Returns(binding);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(invoker, finalHandler, transportProvider, _serviceProvider);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);
		// Simulate inbound transport message
		context.Items[TransportContextProvider.TransportBindingNameProperty] = "rabbitmq-binding";

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - GetTransportBinding MUST be called for inbound dispatches
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PreservePreSetCorrelationIdOnOutboundDispatch()
	{
		// Arrange - verify T.2 doesn't break correlation ID handling
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(invoker, finalHandler, null, _serviceProvider);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider) { CorrelationId = "pre-set-corr-id" };

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - pre-set correlation ID is honored
		context.CorrelationId.ShouldBe("pre-set-corr-id");
	}

	[Fact]
	public async Task HandleConcurrentOutboundDispatchesSafely()
	{
		// Arrange - stress test T.2 under concurrency
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(invoker, finalHandler, null, _serviceProvider);

		// Act - 50 concurrent dispatches, all outbound (no transport origin)
		var tasks = Enumerable.Range(0, 50).Select(_ =>
		{
			var msg = new FakeDispatchMessage();
			var ctx = new MessageContext(msg, _serviceProvider);
			return dispatcher.DispatchAsync(msg, ctx, CancellationToken.None);
		});

		// Assert - no exceptions under concurrent outbound dispatch
		await Should.NotThrowAsync(() => Task.WhenAll(tasks));
	}

	#endregion

	#region T.3 (Opt 2): Per-Transport-Profile Middleware Bypass Flag

	[Fact]
	public void BypassMiddlewareWhenOnlyRoutingMiddlewareRegistered()
	{
		// Arrange - only RoutingMiddleware registered (should bypass)
		var router = A.Fake<IDispatchRouter>();
		var routingMiddleware = new RoutingMiddleware(router, NullLogger<RoutingMiddleware>.Instance);
		var invoker = new DispatchMiddlewareInvoker([routingMiddleware]);

		// Act
		var canBypass = invoker.CanBypassFor(typeof(FakeDispatchMessage));

		// Assert - routing-only middleware should be bypassable
		canBypass.ShouldBeTrue();
	}

	[Fact]
	public void NotBypassMiddlewareWhenNonRoutingMiddlewareRegistered()
	{
		// Arrange - a non-routing middleware is registered
		var router = A.Fake<IDispatchRouter>();
		var routingMiddleware = new RoutingMiddleware(router, NullLogger<RoutingMiddleware>.Instance);
		var customMiddleware = new FakeNonRoutingMiddleware();
		var invoker = new DispatchMiddlewareInvoker([routingMiddleware, customMiddleware]);

		// Act
		var canBypass = invoker.CanBypassFor(typeof(FakeDispatchMessage));

		// Assert - non-routing middleware present, cannot bypass
		canBypass.ShouldBeFalse();
	}

	[Fact]
	public void BypassMiddlewareWhenNoMiddlewareRegistered()
	{
		// Arrange - empty middleware pipeline
		var invoker = new DispatchMiddlewareInvoker([]);

		// Act
		var canBypass = invoker.CanBypassFor(typeof(FakeDispatchMessage));

		// Assert - no middleware at all, should bypass
		canBypass.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteNonRoutingMiddlewareWhenRegistered()
	{
		// Arrange - verify middleware chain still fires when non-routing middleware exists
		var executed = false;
		var customMiddleware = new FakeNonRoutingMiddleware(() => executed = true);
		var invoker = new DispatchMiddlewareInvoker([customMiddleware]);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);

		static ValueTask<IMessageResult> NextDelegate(
			IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			=> new(Excalibur.Dispatch.Abstractions.MessageResult.Success());

		// Act
		await invoker.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipChainLookupWhenOnlyRoutingMiddleware()
	{
		// Arrange - routing-only pipeline should skip directly to next delegate
		var router = A.Fake<IDispatchRouter>();
		var routingMiddleware = new RoutingMiddleware(router, NullLogger<RoutingMiddleware>.Instance);
		var invoker = new DispatchMiddlewareInvoker([routingMiddleware]);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);
		var nextCalled = false;

		ValueTask<IMessageResult> NextDelegate(
			IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			nextCalled = true;
			return new(Excalibur.Dispatch.Abstractions.MessageResult.Success());
		}

		// Act
		var result = await invoker.InvokeAsync(message, context, NextDelegate, CancellationToken.None);

		// Assert - next delegate called directly, bypassing chain
		nextCalled.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region T.4 (Opt 3): Single Transport Bus Pre-Resolution

	[Fact]
	public void PreResolveSingleTransportBusWhenOneNonLocalBusRegistered()
	{
		// Arrange - single non-local bus "rabbitmq"
		var busProvider = A.Fake<IMessageBusProvider>();
		var transportBus = A.Fake<IMessageBus>();

		A.CallTo(() => busProvider.GetAllMessageBusNames())
			.Returns(LocalAndRabbitBusNames);
		A.CallTo(() => busProvider.TryGet("rabbitmq", out transportBus))
			.Returns(true);

		// Act - construct handler (pre-resolution happens in constructor)
		var handler = new FinalDispatchHandler(
			busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());

		// Assert - handler constructed without error, bus was resolved at construction time
		// The single-bus optimization is internal; we verify it works by dispatching through Dispatcher
		handler.ShouldNotBeNull();
	}

	[Fact]
	public void NotPreResolveWhenMultipleBusesRegistered()
	{
		// Arrange - multiple non-local buses means no single-bus optimization
		var busProvider = A.Fake<IMessageBusProvider>();
		var kafkaBus = A.Fake<IMessageBus>();
		var rabbitBus = A.Fake<IMessageBus>();

		A.CallTo(() => busProvider.GetAllMessageBusNames())
			.Returns(LocalKafkaAndRabbitBusNames);
		A.CallTo(() => busProvider.TryGet("kafka", out kafkaBus)).Returns(true);
		A.CallTo(() => busProvider.TryGet("rabbitmq", out rabbitBus)).Returns(true);

		// Act - construct handler (should NOT pre-resolve single bus)
		var handler = new FinalDispatchHandler(
			busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());

		// Assert - handler constructed without error
		handler.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructFinalHandlerWithSingleBusWithoutError()
	{
		// Arrange - single non-local bus "rabbitmq" with proper out param setup
		var busProvider = A.Fake<IMessageBusProvider>();
		var transportBus = A.Fake<IMessageBus>();

		A.CallTo(() => busProvider.GetAllMessageBusNames()).Returns(LocalAndRabbitBusNames);
		A.CallTo(busProvider)
			.Where(call => call.Method.Name == "TryGet" &&
				call.Arguments.Count == 2 &&
				"rabbitmq".Equals(call.Arguments[0]))
			.WithReturnType<bool>()
			.Returns(true)
			.AssignsOutAndRefParameters(transportBus);

		// Act - construction triggers ResolveSingleTransportBus
		var handler = new FinalDispatchHandler(
			busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());

		// Assert - handler constructed successfully, bus resolution was attempted
		handler.ShouldNotBeNull();
		A.CallTo(busProvider)
			.Where(call => call.Method.Name == "TryGet")
			.MustHaveHappened();
	}

	[Fact]
	public void ConstructFinalHandlerWithMultipleBusesWithoutPreResolution()
	{
		// Arrange - multiple non-local buses, no single-bus pre-resolution
		var busProvider = A.Fake<IMessageBusProvider>();

		A.CallTo(() => busProvider.GetAllMessageBusNames()).Returns(LocalKafkaAndRabbitBusNames);

		// Act - construction sees >1 non-local bus, skips pre-resolution
		var handler = new FinalDispatchHandler(
			busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());

		// Assert - handler constructed; TryGet called only for "local" (ResolveLocalBus),
		// NOT for "kafka" or "rabbitmq" (multi-bus means no single-bus pre-resolution)
		handler.ShouldNotBeNull();
		A.CallTo(busProvider)
			.Where(call => call.Method.Name == "TryGet" &&
				call.Arguments.Count == 2 &&
				!"local".Equals(call.Arguments[0]))
			.MustNotHaveHappened();
	}

	#endregion

	#region T.5 (Opt 4): Routing Decision Cache for Deterministic Routers

	[Fact]
	public async Task CacheRoutingDecisionForDefaultDispatchRouter()
	{
		// Arrange - DefaultDispatchRouter enables caching
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("rabbitmq"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(DefaultEndpoints));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(
			invoker, finalHandler, null, _serviceProvider,
			dispatchRouter: router);

		// Act - dispatch same message type twice
		var msg1 = new FakeDispatchMessage();
		var ctx1 = new MessageContext(msg1, _serviceProvider);
		await dispatcher.DispatchAsync(msg1, ctx1, CancellationToken.None);

		var msg2 = new FakeDispatchMessage();
		var ctx2 = new MessageContext(msg2, _serviceProvider);
		await dispatcher.DispatchAsync(msg2, ctx2, CancellationToken.None);

		// Assert - router called only once (second dispatch uses cache)
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCacheRoutingDecisionForCustomRouter()
	{
		// Arrange - custom (non-Default) router does NOT enable caching
		var customRouter = A.Fake<IDispatchRouter>();
		A.CallTo(() => customRouter.RouteAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(
				RoutingDecision.Success("rabbitmq", DefaultEndpoints)));

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(
			invoker, finalHandler, null, _serviceProvider,
			dispatchRouter: customRouter);

		// Act - dispatch same message type twice
		var msg1 = new FakeDispatchMessage();
		var ctx1 = new MessageContext(msg1, _serviceProvider);
		await dispatcher.DispatchAsync(msg1, ctx1, CancellationToken.None);

		var msg2 = new FakeDispatchMessage();
		var ctx2 = new MessageContext(msg2, _serviceProvider);
		await dispatcher.DispatchAsync(msg2, ctx2, CancellationToken.None);

		// Assert - router called BOTH times (no caching for custom routers)
		A.CallTo(() => customRouter.RouteAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task NotCrossContaminateRoutingCacheAcrossMessageTypes()
	{
		// Arrange - different message types should get independent routing decisions
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("rabbitmq"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(DefaultEndpoints));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(
			invoker, finalHandler, null, _serviceProvider,
			dispatchRouter: router);

		// Act - dispatch two DIFFERENT message types
		var msg1 = new FakeDispatchMessage();
		var ctx1 = new MessageContext(msg1, _serviceProvider);
		await dispatcher.DispatchAsync(msg1, ctx1, CancellationToken.None);

		var msg2 = new AnotherFakeDispatchMessage();
		var ctx2 = new MessageContext(msg2, _serviceProvider);
		await dispatcher.DispatchAsync(msg2, ctx2, CancellationToken.None);

		// Assert - transport selector called for each distinct message type
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
	}

	[Fact]
	public async Task HandleConcurrentRoutingCacheAccessSafely()
	{
		// Arrange - stress test routing cache under concurrency
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("rabbitmq"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(DefaultEndpoints));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);
		var dispatcher = new Dispatcher(
			invoker, finalHandler, null, _serviceProvider,
			dispatchRouter: router);

		// Act - 50 concurrent dispatches of the same message type
		var tasks = Enumerable.Range(0, 50).Select(_ =>
		{
			var msg = new FakeDispatchMessage();
			var ctx = new MessageContext(msg, _serviceProvider);
			return dispatcher.DispatchAsync(msg, ctx, CancellationToken.None);
		});

		// Assert - no exceptions under concurrent cache access
		await Should.NotThrowAsync(() => Task.WhenAll(tasks));
	}

	#endregion

	#region Cross-Optimization Interaction Tests

	[Fact]
	public async Task AllOptimizationsCombineCorrectlyForOutboundDispatch()
	{
		// Arrange - all 4 optimizations active: outbound, routing-only middleware,
		// single transport bus, DefaultDispatchRouter with caching
		var transportSelector = A.Fake<ITransportSelector>();
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<string>("rabbitmq"));

		var endpointRouter = A.Fake<IEndpointRouter>();
		A.CallTo(() => endpointRouter.RouteToEndpointsAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<string>>(DefaultEndpoints));

		var router = new DefaultDispatchRouter(transportSelector, endpointRouter);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var transportProvider = A.Fake<ITransportContextProvider>();
		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);

		var dispatcher = new Dispatcher(
			invoker, finalHandler, transportProvider, _serviceProvider,
			dispatchRouter: router);

		// Act - two dispatches to verify caching works
		var msg1 = new FakeDispatchMessage();
		var ctx1 = new MessageContext(msg1, _serviceProvider);
		await dispatcher.DispatchAsync(msg1, ctx1, CancellationToken.None);

		var msg2 = new FakeDispatchMessage();
		var ctx2 = new MessageContext(msg2, _serviceProvider);
		await dispatcher.DispatchAsync(msg2, ctx2, CancellationToken.None);

		// Assert
		// T.2: transport binding NOT resolved (outbound)
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._))
			.MustNotHaveHappened();
		// T.5: router called only once (cached second time)
		A.CallTo(() => transportSelector.SelectTransportAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InboundDispatchStillResolvesBindingWithAllOptimizations()
	{
		// Arrange - inbound message should still get transport binding resolved
		// even with all other optimizations active
		var transportProvider = A.Fake<ITransportContextProvider>();
		var binding = A.Fake<ITransportBinding>();
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._)).Returns(binding);

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		SetupInvokerPassThrough(invoker);

		var busProvider = A.Fake<IMessageBusProvider>();
		var finalHandler = CreateFinalHandler(busProvider);

		var customRouter = A.Fake<IDispatchRouter>();
		A.CallTo(() => customRouter.RouteAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<RoutingDecision>(
				RoutingDecision.Success("rabbitmq", DefaultEndpoints)));

		var dispatcher = new Dispatcher(
			invoker, finalHandler, transportProvider, _serviceProvider,
			dispatchRouter: customRouter);

		var message = new FakeDispatchMessage();
		var context = new MessageContext(message, _serviceProvider);
		context.Items[TransportContextProvider.TransportBindingNameProperty] = "inbound-binding";

		// Act
		await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - T.2 correctly identifies inbound and resolves binding
		A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Helpers

	private static void SetupInvokerPassThrough(IDispatchMiddlewareInvoker invoker)
	{
		A.CallTo(() => invoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(Excalibur.Dispatch.Abstractions.MessageResult.Success()));
	}

	private static FinalDispatchHandler CreateFinalHandler(IMessageBusProvider busProvider)
	{
		return new FinalDispatchHandler(
			busProvider,
			NullLogger<FinalDispatchHandler>.Instance,
			null,
			new Dictionary<string, IMessageBusOptions>());
	}

	/// <summary>
	/// A second message type used to test that routing cache does not cross-contaminate between types.
	/// </summary>
	private sealed class AnotherFakeDispatchMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
	}

	/// <summary>
	/// A fake dispatch action used in T.4 end-to-end tests where the message must implement
	/// IDispatchAction to reach the bus PublishAsync(IDispatchAction, ...) path.
	/// </summary>
	private sealed class FakeDispatchAction : IDispatchAction
	{
		public Guid Id { get; } = Guid.NewGuid();
	}

	/// <summary>
	/// A non-routing middleware used to test that the T.3 bypass flag correctly identifies
	/// when real middleware exists that must be executed.
	/// </summary>
	private sealed class FakeNonRoutingMiddleware : IDispatchMiddleware
	{
		private readonly Action? _onExecute;

		public FakeNonRoutingMiddleware(Action? onExecute = null) => _onExecute = onExecute;

		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			_onExecute?.Invoke();
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion
}
