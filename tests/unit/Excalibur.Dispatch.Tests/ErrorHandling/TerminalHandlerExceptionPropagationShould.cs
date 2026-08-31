// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestFakes;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// An exception thrown by a message handler must reach the pipeline above the terminal dispatch handler,
/// so a consumer's exception mapper or typed exception handler can observe it -- which is the contract
/// <see cref="IActionHandler{TAction}"/> already documents, and the behaviour a dispatch with no middleware
/// registered already has.
/// </summary>
/// <remarks>
/// <para>
/// The terminal handler used to catch every handler fault and return a generic 500 failed result. Two
/// consequences: no middleware could map the fault, and the 500 it stamped was classified transient by the
/// retry policy, so a permanently-failing non-idempotent handler ran once per attempt.
/// </para>
/// <para>
/// The discriminator is WHICH BUS threw, never which method caught it: a fault out of the in-process bus is
/// a handler fault and propagates; a fault out of a transport bus is a delivery outcome and stays a failed
/// result. Routing by bus NAME can land an in-process dispatch in the transport-shaped code path, so every
/// guard tests the bus TYPE.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
public sealed class TerminalHandlerExceptionPropagationShould
{
	private const string MappedProblemType = "urn:test:mapped-by-the-consumers-mapper";
	private const string HandlerErrorProblemType = "urn:dispatch:error:handler-error";
	private const string RecoveredMarker = "recovered-by-the-consumers-typed-handler";
	private const string FaultMessage = "the consumer's handler rejected this input";

	private sealed record ProbeAction : IDispatchAction;

	private sealed record ProbeEvent : IDispatchEvent;

	/// <summary>Records that it ran, so the fan-out's liveness arm can assert the sibling was not abandoned.</summary>
	private sealed class QuietEventHandler(InvocationCounter counter) : IEventHandler<ProbeEvent>
	{
		public Task HandleAsync(ProbeEvent eventMessage, CancellationToken cancellationToken)
		{
			counter.Increment();
			return Task.CompletedTask;
		}
	}

	/// <summary>Throws a DIFFERENT fault, so a two-fault fan-out has no single exception to surface.</summary>
	private sealed class OtherFailingEventHandler : IEventHandler<ProbeEvent>
	{
		public Task HandleAsync(ProbeEvent eventMessage, CancellationToken cancellationToken) =>
			throw new TimeoutException("the second handler also rejected this input");
	}

	/// <summary>Counts real handler invocations. A retry that re-runs a permanent failure shows up here.</summary>
	private sealed class InvocationCounter
	{
		private int _count;

		public int Count => Volatile.Read(ref _count);

		public void Increment() => _ = Interlocked.Increment(ref _count);
	}

	/// <summary>
	/// Throws <see cref="ArgumentException"/> -- classified PERMANENT by the shared failure classifier, so a
	/// correct pipeline runs the handler once. At HEAD the terminal handler converted it to a 500, which the
	/// retry policy classifies transient, and it ran once per attempt.
	/// </summary>
	private sealed class PermanentlyFailingHandler(InvocationCounter counter) : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken)
		{
			counter.Increment();
			throw new ArgumentException(FaultMessage);
		}
	}

	/// <summary>Converts the fault into a success, which no other stage can produce.</summary>
	private sealed class RecoveringHandler : ITypedExceptionHandler<ArgumentException>
	{
		public ValueTask<ExceptionHandlerResult> HandleAsync(
			ArgumentException exception,
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(ExceptionHandlerResult.Handled(
				MessageResult.Success($"{RecoveredMarker}|{exception.GetType().Name}|{exception.Message}")));
	}

	/// <summary>A middleware that changes nothing. Registering it must not change the dispatch contract.</summary>
	private sealed class PassThroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	private static ServiceProvider Build(Action<IDispatchBuilder>? configure, out InvocationCounter counter)
	{
		var invocations = new InvocationCounter();
		counter = invocations;

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(invocations);
		_ = services.AddDispatch(dispatch => configure?.Invoke(dispatch));
		_ = services.AddTransient<IActionHandler<ProbeAction>, PermanentlyFailingHandler>();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// ARM 1 -- LIVENESS. A consumer's own <see cref="IExceptionMapper"/> mapping must reach the caller for a
	/// fault thrown by their message handler. The explicit <c>ShouldNotBe</c> is what makes this arm
	/// non-vacuous: the generic handler-error problem type is what HEAD returned, and a mapper that silently
	/// never ran would otherwise still produce a failed result.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersExceptionMapperTheHandlersException()
	{
		await using var provider = Build(
			dispatch => _ = dispatch
				.WithExceptionMapping(mapping => _ = mapping.Map<ArgumentException>(ex => new MessageProblemDetails
				{
					Type = MappedProblemType,
					Title = "Mapped by the consumer",
					Status = 422,
					Detail = ex.Message,
				}))
				.UseExceptionMapping(),
			out _);

		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldNotBe(
			HandlerErrorProblemType,
			"the terminal dispatch handler must not convert a handler fault into a generic problem before the "
			+ "consumer's mapper can see it");
		result.ProblemDetails.Type.ShouldBe(MappedProblemType);
		result.ProblemDetails.Detail.ShouldBe(FaultMessage);
	}

	/// <summary>
	/// ARM 2 -- LIVENESS. Same for <see cref="ITypedExceptionHandler{TException}"/>: the sentinel asserted is
	/// the one the consumer's handler returned, and the exception's TYPE and MESSAGE must survive intact --
	/// a wrapper exception would satisfy a looser assertion while breaking the consumer's <c>catch</c>.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersTypedHandlerTheHandlersExceptionUnwrapped()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new InvocationCounter());
		_ = services.AddDispatch(dispatch => _ = dispatch.UseTypedExceptionHandling());
		_ = services.AddTransient<IActionHandler<ProbeAction>, PermanentlyFailingHandler>();
		_ = services.AddSingleton<ITypedExceptionHandler<ArgumentException>, RecoveringHandler>();

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			"only the consumer's typed handler can turn this fault into a success, so a success proves it ran");
		var sentinel = (result as IMessageResult<string>)?.ReturnValue;
		sentinel.ShouldBe(
			$"{RecoveredMarker}|{nameof(ArgumentException)}|{FaultMessage}",
			"the handler's own exception must arrive unwrapped, with its type and message intact");
	}

	/// <summary>
	/// ARM 3 -- SAFETY. A transport bus that throws still yields a failed result. Without this arm the fix
	/// over-corrects and silently breaks the delivery contract for every transport.
	/// </summary>
	[Fact]
	public async Task StillReturnAFailedResultWhenANonLocalBusThrows()
	{
		var busProvider = A.Fake<IMessageBusProvider>();
		var transportBus = A.Fake<IMessageBus>();

		IMessageBus? outBus = transportBus;
		_ = A.CallTo(() => busProvider.TryGet("TestBus", out outBus))
			.Returns(true)
			.AssignsOutAndRefParameters(transportBus);
		_ = A.CallTo(() => transportBus.PublishAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.ThrowsAsync(new ArgumentException(FaultMessage));

		var handler = new FinalDispatchHandler(
			busProvider,
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>());

		var result = await handler.HandleAsync(
			new ProbeAction(),
			ContextRoutedTo("TestBus"),
			TestContext.Current.CancellationToken);

		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Status.ShouldBe(500);
		result.ProblemDetails.Detail.ShouldContain(FaultMessage);
	}

	/// <summary>
	/// ARM 4 -- SUBSTITUTABILITY. Registering a middleware must not change how a handler fault reaches the
	/// caller. This is the invariant the swallow broke, and it generalises past exception mapping: with no
	/// middleware the dispatch bypassed the terminal handler's catch and threw, with any middleware it did not.
	/// </summary>
	[Fact]
	public async Task ReachTheCallerTheSameWayWithAndWithoutMiddleware()
	{
		await using var bare = Build(configure: null, out _);
		await using var withMiddleware = Build(
			dispatch => _ = dispatch.UseMiddleware<PassThroughMiddleware>(),
			out _);

		var bareFault = await Should.ThrowAsync<ArgumentException>(async () =>
			await bare.GetRequiredService<IDispatcher>()
				.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken));

		var middlewareFault = await Should.ThrowAsync<ArgumentException>(async () =>
			await withMiddleware.GetRequiredService<IDispatcher>()
				.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken));

		middlewareFault.GetType().ShouldBe(bareFault.GetType());
		middlewareFault.Message.ShouldBe(bareFault.Message);
	}

	/// <summary>
	/// ARM 5 -- THE PROXY-PREDICATE TRAP. A consumer who routes explicitly to the in-process bus by NAME lands
	/// in the transport-shaped code path with the in-process bus resolved. A guard written against the method
	/// rather than the bus type keeps swallowing here, and every other arm still passes.
	/// </summary>
	[Fact]
	public async Task PropagateWhenTheInProcessBusIsReachedByAnExplicitRoute()
	{
		await using var provider = Build(configure: null, out _);

		var handler = new FinalDispatchHandler(
			provider.GetRequiredService<IMessageBusProvider>(),
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>());

		var faulted = await Should.ThrowAsync<ArgumentException>(async () =>
			await handler.HandleAsync(
				new ProbeAction(),
				ContextRoutedTo("local"),
				TestContext.Current.CancellationToken));

		faulted.Message.ShouldBe(FaultMessage);
	}

	/// <summary>
	/// ARM 6 -- THE DUPLICATE-SIDE-EFFECT DEFECT. A permanently-failing handler under the default middleware
	/// stack must run exactly once. Counting invocations, not asserting a result shape, is what makes this
	/// arm about the harm: at HEAD the terminal handler stamped 500 on a permanent fault, the retry policy
	/// read 500 as transient, and a non-idempotent handler took its side effect once per attempt.
	/// </summary>
	[Fact]
	public async Task InvokeAPermanentlyFailingHandlerExactlyOnceUnderTheDefaultStack()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var counter = new InvocationCounter();
		_ = services.AddSingleton(counter);
		_ = services.Configure<RetryOptions>(options =>
		{
			options.MaxAttempts = 3;
			options.BaseDelay = TimeSpan.FromMilliseconds(1);
		});
		_ = services.AddExceptionMapping();
		services.TryAddSingleton<IValidatorResolver, NoOpValidatorResolver>();
		services.TryAddSingleton<IMessageValidationService, NoOpValidationService>();
		services.TryAddSingleton<ValidationMiddleware>();
		services.TryAddSingleton<LoggingMiddleware>();
		services.TryAddSingleton<TimeoutMiddleware>();
		services.TryAddSingleton<RetryMiddleware>();
		services.TryAddSingleton<ExceptionMappingMiddleware>();
		_ = services.AddDispatch(dispatch =>
			_ = dispatch.ConfigurePipeline("default", pipeline => _ = pipeline.UseDefaults()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, PermanentlyFailingHandler>();

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		_ = await dispatcher.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		counter.Count.ShouldBe(
			1,
			"an ArgumentException is a permanent fault; retrying it re-runs the handler's side effects for a "
			+ "failure that cannot succeed");
	}


	/// <summary>Throws from the in-process bus so a fan-out has one failing leg.</summary>
	private sealed class ThrowingEventHandler : IEventHandler<ProbeEvent>
	{
		public Task HandleAsync(ProbeEvent domainEvent, CancellationToken cancellationToken) =>
			throw new ArgumentException(FaultMessage);
	}

	/// <summary>Records that it was published to, so an abandoned fan-out leg is visible.</summary>
	private sealed class RecordingTransportBus : IMessageBus
	{
		private int _received;

		public int Received => Volatile.Read(ref _received);

		public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken) =>
			Record();

		public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken) =>
			Record();

		public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken) =>
			Record();

		private Task Record()
		{
			_ = Interlocked.Increment(ref _received);
			return Task.CompletedTask;
		}
	}

	/// <summary>Throws, and is NOT a LocalMessageBus -- what a consumer gets by claiming the local name first.</summary>
	private sealed class ThrowingTransportBus : IMessageBus
	{
		public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken) =>
			throw new ArgumentException(FaultMessage);

		public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken) =>
			throw new ArgumentException(FaultMessage);

		public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken) =>
			throw new ArgumentException(FaultMessage);
	}

	/// <summary>
	/// ARM 7 -- FAN-OUT. A publish to several buses is a fan-out, not a call: one leg's fault must not abandon
	/// the others. Both orderings are asserted because a mid-loop escape is order-dependent -- with the local
	/// leg first it abandons everything, with the local leg last it abandons nothing, and either arm alone
	/// passes against the broken code.
	/// </summary>
	/// <remarks>
	/// Per-route <c>DeliveryStatus</c> is built inside the handler and not reachable from here, so the arm
	/// asserts it through the surface a consumer sees: the failure detail names the failing bus and only the
	/// failing bus.
	/// </remarks>
	[Theory]
	[InlineData("local", "transportA")]
	[InlineData("transportA", "local")]
	public async Task PublishToEveryFanOutLegEvenWhenTheInProcessLegThrows(string first, string second)
	{
		await using var provider = BuildWithEventHandler();
		var transport = new RecordingTransportBus();
		var handler = new FinalDispatchHandler(
			TwoBusProvider(provider.GetRequiredService<IMessageBusProvider>(), transport),
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>());

		var context = ContextRoutedToMany(first, second);

		var result = await handler.HandleAsync(new ProbeEvent(), context, TestContext.Current.CancellationToken);

		transport.Received.ShouldBe(
			1,
			"a fault on the in-process leg must not abandon the remaining legs, whatever order they are listed in");
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldContain($"local: {FaultMessage}");
		result.ProblemDetails.Detail.Contains("transportA", StringComparison.Ordinal).ShouldBeFalse(
			"the transport leg succeeded, so it must not appear among the failures");
	}

	/// <summary>
	/// ARM 8 -- THE LOCAL NAME IS NOT THE LOCAL TYPE. Bus registration is first-wins, so a consumer who claims
	/// the local name before calling AddDispatch() puts their own bus on the fast path. Its fault is a delivery
	/// outcome, not a handler's, and must stay a failed result.
	/// </summary>
	[Fact]
	public async Task StillReturnAFailedResultWhenTheBusNamedLocalIsNotTheInProcessBus()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new InvocationCounter());
		_ = services.AddMessageBus("local", isRemote: false, _ => new ThrowingTransportBus());
		_ = services.AddDispatch();
		_ = services.AddTransient<IActionHandler<ProbeAction>, PermanentlyFailingHandler>();
		await using var provider = services.BuildServiceProvider();

		var handler = new FinalDispatchHandler(
			provider.GetRequiredService<IMessageBusProvider>(),
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>());

		var result = await handler.HandleAsync(
			new ProbeAction(),
			new FakeMessageContext { MessageId = Guid.NewGuid().ToString() },
			TestContext.Current.CancellationToken);

		result.Succeeded.ShouldBeFalse(
			"the bus under the local name is a transport, so its fault is a delivery outcome and stays a result");
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Detail.ShouldContain(FaultMessage);
	}

	/// <summary>
	/// ARM 8, POSITIVE CONTROL -- the same fast path with the REAL in-process bus still propagates. Without this
	/// the guard above is satisfied by deleting propagation from the fast path altogether.
	/// </summary>
	[Fact]
	public async Task StillPropagateOnTheFastPathWhenTheBusNamedLocalIsTheInProcessBus()
	{
		await using var provider = Build(configure: null, out _);

		var handler = new FinalDispatchHandler(
			provider.GetRequiredService<IMessageBusProvider>(),
			NullLoggerFactory.Instance.CreateLogger<FinalDispatchHandler>(),
			retryPolicy: null,
			new Dictionary<string, MessageBusOptions>());

		var faulted = await Should.ThrowAsync<ArgumentException>(async () =>
			await handler.HandleAsync(
				new ProbeAction(),
				new FakeMessageContext { MessageId = Guid.NewGuid().ToString() },
				TestContext.Current.CancellationToken));

		faulted.Message.ShouldBe(FaultMessage);
	}

	/// <summary>
	/// Registers exactly ONE plan for <see cref="ProbeEvent"/>. The lambda overload is deliberate: the
	/// parameterless call binds the <c>params Assembly[]</c> overload, which scans the entry assembly, finds
	/// this handler a second time, and gives one handler TWO dispatch plans -- so the single throwing handler
	/// produced two faults and took the aggregated path instead of the single-fault one.
	/// </summary>
	private static ServiceProvider BuildWithEventHandler()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(configure: null);
		_ = services.AddTransient<IEventHandler<ProbeEvent>, ThrowingEventHandler>();
		return services.BuildServiceProvider();
	}

	/// <summary>Serves the real in-process bus under its own name and a recording fake under a transport name.</summary>
	private static IMessageBusProvider TwoBusProvider(IMessageBusProvider real, IMessageBus transport)
	{
		var provider = A.Fake<IMessageBusProvider>();

		_ = real.TryGet("local", out var localBus);
		IMessageBus? outLocal = localBus;
		_ = A.CallTo(() => provider.TryGet("local", out outLocal)).Returns(true).AssignsOutAndRefParameters(localBus);

		IMessageBus? outTransport = transport;
		_ = A.CallTo(() => provider.TryGet("transportA", out outTransport)).Returns(true).AssignsOutAndRefParameters(transport);

		return provider;
	}

	private static FakeMessageContext ContextRoutedToMany(params string[] busNames)
	{
		var context = new FakeMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
		};
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success(busNames[0], busNames);
		return context;
	}

	private static FakeMessageContext ContextRoutedTo(string busName)
	{
		var context = new FakeMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
		};
		context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success(busName, [busName]);
		return context;
	}

	/// <summary>
	/// ARM 9 -- THE HANDLER-COUNT ASYMMETRY. A consumer registers one exception handler for their domain
	/// fault. Registering a SECOND event handler must not stop it matching. The fan-out collects faults, so
	/// a sole fault used to arrive wrapped in an <see cref="AggregateException"/> and the type walk looked
	/// for a handler of that wrapper instead of the fault the consumer wrote.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersTypedHandlerTheFaultFromAnEventWithTwoHandlers()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var counter = new InvocationCounter();
		_ = services.AddSingleton(counter);
		_ = services.AddDispatch(dispatch => _ = dispatch.UseTypedExceptionHandling());
		_ = services.AddTransient<IEventHandler<ProbeEvent>, ThrowingEventHandler>();
		_ = services.AddTransient<IEventHandler<ProbeEvent>, QuietEventHandler>();
		_ = services.AddSingleton<ITypedExceptionHandler<ArgumentException>, RecoveringHandler>();

		await using var provider = services.BuildServiceProvider();

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeEvent(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			"only the consumer's typed handler can turn this fault into a success, so a success proves it ran");
		var sentinel = (result as IMessageResult<string>)?.ReturnValue;
		sentinel.ShouldBe(
			$"{RecoveredMarker}|{nameof(ArgumentException)}|{FaultMessage}",
			"the handler's own exception must arrive unwrapped even though a second handler was registered");
		counter.Count.ShouldBe(1, "the non-faulting sibling must still have run -- fault-independence");
	}

	/// <summary>
	/// ARM 10 -- the same asymmetry through <see cref="IExceptionMapper"/>, which walks the thrown type the
	/// same way. Asserting the mapped problem type rules out the generic handler-error problem HEAD produced.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersExceptionMapperTheFaultFromAnEventWithTwoHandlers()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new InvocationCounter());
		_ = services.AddDispatch(dispatch => _ = dispatch
			.WithExceptionMapping(mapping => _ = mapping.Map<ArgumentException>(ex => new MessageProblemDetails
			{
				Type = MappedProblemType,
				Title = "Mapped by the consumer",
				Status = 422,
				Detail = ex.Message,
			}))
			.UseExceptionMapping());
		_ = services.AddTransient<IEventHandler<ProbeEvent>, ThrowingEventHandler>();
		_ = services.AddTransient<IEventHandler<ProbeEvent>, QuietEventHandler>();

		await using var provider = services.BuildServiceProvider();

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeEvent(), TestContext.Current.CancellationToken);

		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldNotBe(
			HandlerErrorProblemType,
			"a second registered handler must not stop the consumer's mapper matching their own exception");
		result.ProblemDetails.Type.ShouldBe(MappedProblemType);
		result.ProblemDetails.Detail.ShouldBe(FaultMessage);
	}

	/// <summary>
	/// ARM 11 -- SAFETY, and the arm that keeps ARM 9 from over-correcting into "never aggregate". Two
	/// handlers faulting have no single exception to surface, so the aggregate stays and EVERY fault is in
	/// it. Without this arm, unwrapping unconditionally (or taking the first fault) would still pass.
	/// </summary>
	[Fact]
	public async Task StillSurfaceEveryFaultWhenMoreThanOneEventHandlerThrows()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new InvocationCounter());
		_ = services.AddDispatch(configure: null);
		_ = services.AddTransient<IEventHandler<ProbeEvent>, ThrowingEventHandler>();
		_ = services.AddTransient<IEventHandler<ProbeEvent>, OtherFailingEventHandler>();

		await using var provider = services.BuildServiceProvider();

		var aggregate = await Should.ThrowAsync<AggregateException>(async () =>
			await provider.GetRequiredService<IDispatcher>()
				.DispatchAsync(new ProbeEvent(), TestContext.Current.CancellationToken));

		aggregate.InnerExceptions.ShouldContain(e => e is ArgumentException);
		aggregate.InnerExceptions.ShouldContain(e => e is TimeoutException);
	}
}
