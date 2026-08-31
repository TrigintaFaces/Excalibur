// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Dispatching an action with no registered handler must fail loudly, on every path, with a message that says how to fix it.
/// </summary>
/// <remarks>
/// Forgetting to register a handler -- keeping it in another assembly, or never calling AddDispatch for it -- is the commonest
/// first-run mistake, and it is a configuration fault rather than a dispatch outcome: it cannot vary per request and the caller cannot
/// recover from it. Reported as a failed result it is indistinguishable from a handler that rejected the request, so a caller that maps
/// failure to 400 blames the end user for the operator's missing registration. These arms hold every dispatch overload to the same
/// behaviour, and the last one holds the line in the other direction: a handler that ran and threw reaches the caller as its OWN
/// exception, so the two faults stay distinguishable and "make the missing handler throw" cannot be satisfied by throwing the same
/// thing for both.
/// </remarks>
public sealed class MissingHandlerDiagnosticsShould
{
	private sealed record OrphanAction : IDispatchAction;

	private sealed record OrphanQuery : IDispatchAction<string>;

	private sealed record BoomAction : IDispatchAction;

	private sealed record OrphanDocument : IDispatchDocument;

	private sealed record CachedAction : IDispatchAction;

	/// <summary>Stands in for the caching middleware on a hit: stages a result in context and never needs a handler.</summary>
	private sealed class CacheHitMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			if (message is CachedAction)
			{
				context.Items["Dispatch:Result"] = MessageResult.Success();
				context.Items["Dispatch:CacheHit"] = true;
			}

			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Counts how many times the pipeline reached the stage below retry, and optionally fails there with a retryable fault.
	/// </summary>
	/// <remarks>
	/// Registered at <see cref="DispatchMiddlewareStage.End" />, which sorts after the <see cref="DispatchMiddlewareStage.ErrorHandling" />
	/// stage retry occupies, so it is retry's own loop that invokes it: one increment per attempt, whatever the outcome.
	/// </remarks>
	/// <summary> Shared, DI-resolved state. UseMiddleware constructs the middleware itself, so the arm cannot
	/// hold the instance -- it holds this instead. </summary>
	private sealed class AttemptCounter
	{
		public int Attempts { get; set; }

		public bool FailWithRetryableFault { get; init; }
	}

	private sealed class AttemptCountingMiddleware(AttemptCounter counter) : IDispatchMiddleware
	{
		private readonly bool failWithRetryableFault = counter.FailWithRetryableFault;

		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			counter.Attempts++;

			if (failWithRetryableFault)
			{
				// Absent from the default NonRetryableExceptions floor, so retry treats it as transient.
				throw new TimeoutException("transient");
			}

			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class BoomHandler : IActionHandler<BoomAction>
	{
		public Task HandleAsync(BoomAction action, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("handler blew up");
	}

	private static ServiceProvider Build(bool withMiddleware)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			if (withMiddleware)
			{
				// A real stage, registered the way a consumer registers one. Its presence flips the dispatcher off its
				// ultra-local path and onto the middleware pipeline, so the two arms cover both routes to the local bus.
				_ = dispatch.UseValidation();
			}
		});

		_ = services.AddTransient<IActionHandler<BoomAction>, BoomHandler>();

		return services.BuildServiceProvider();
	}

	private static void ShouldNameTheTypeAndBothRemedies(Exception exception)
	{
		// The fully-qualified name, because a consumer with two same-named actions in different namespaces cannot act on the short one.
		exception.Message.ShouldContain(typeof(OrphanAction).FullName!);

		// Both ways out, because which one applies depends on where the consumer keeps handlers.
		exception.Message.ShouldContain("AddHandlersFromAssembly");
		exception.Message.ShouldContain("AddTransient<IActionHandler<");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowFromTheContextLessOverload(bool withMiddleware)
	{
		// The overload the getting-started guide teaches. Without middleware it takes the ultra-local path, which used to catch the
		// fault and hand back a failed result carrying nine words and an unqualified type name.
		await using var provider = Build(withMiddleware);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(new OrphanAction(), TestContext.Current.CancellationToken));

		ShouldNameTheTypeAndBothRemedies(exception);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowFromTheExplicitContextOverload(bool withMiddleware)
	{
		await using var provider = Build(withMiddleware);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var action = new OrphanAction();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(action, new MessageContext(action, provider), TestContext.Current.CancellationToken));

		ShouldNameTheTypeAndBothRemedies(exception);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowWhenTheActionExpectsAResponse(bool withMiddleware)
	{
		// The response-carrying overload has its own dispatch path and its own catch, so it needs its own arm.
		await using var provider = Build(withMiddleware);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync<OrphanQuery, string>(new OrphanQuery(), TestContext.Current.CancellationToken));

		exception.Message.ShouldContain(typeof(OrphanQuery).FullName!);
		exception.Message.ShouldContain("AddHandlersFromAssembly");
		exception.Message.ShouldContain("AddTransient<IActionHandler<");
	}

	[Fact]
	public async Task ThrowForAnUnregisteredDocumentToo()
	{
		// Documents reach the same local bus by a sibling path with its own catch. Without this arm the change would leave actions
		// throwing and documents returning a failed result -- a split the framework did not have before and no consumer could predict.
		await using var provider = Build(withMiddleware: false);
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var document = new OrphanDocument();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(document, new MessageContext(document, provider), TestContext.Current.CancellationToken));

		exception.Message.ShouldContain(typeof(OrphanDocument).FullName!);
		exception.Message.ShouldContain("AddTransient<IDocumentHandler<");
	}

	[Fact]
	public async Task ServeACacheHitThatNeedsNoHandler()
	{
		// A cache hit is answered from the context and never reaches the bus, so it legitimately runs with no handler registered.
		// An earlier revision of the registration check ran before this was considered and turned every such hit into a throw.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton<IDispatchMiddleware, CacheHitMiddleware>();
		await using var provider = services.BuildServiceProvider();

		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var action = new CachedAction();

		var result = await dispatcher.DispatchAsync(action, new MessageContext(action, provider), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue("a cache hit is served from the context and must not require a registered handler");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StillDistinguishAThrowingHandlerFromAMissingOne(bool withMiddleware)
	{
		// The counterweight. "Make the missing handler throw" must not be satisfied by throwing the same thing for a handler that ran
		// and rejected the request -- the caller has to tell an operator's missing registration from its own domain fault. Both now
		// reach the caller as exceptions, so the arm asserts they carry DIFFERENT diagnostics.
		//
		// The parameter is the substitutability half: registering a middleware must not change how the fault arrives.
		await using var provider = Build(withMiddleware);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var faulted = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(new BoomAction(), TestContext.Current.CancellationToken));

		faulted.Message.ShouldContain("handler blew up");
		faulted.Message.Contains("AddTransient<IActionHandler<", StringComparison.Ordinal).ShouldBeFalse(
			"a handler that ran and threw must not be reported as a missing registration");
	}
	private static ServiceProvider BuildWith(Action<IDispatchBuilder> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch => configure(dispatch));
		_ = services.AddTransient<IActionHandler<BoomAction>, BoomHandler>();

		return services.BuildServiceProvider();
	}

	private static Action<IDispatchBuilder> PipelineNamed(string name) => name switch
	{
		"retry" => builder => _ = builder.UseRetry(),
		"exception-mapping" => builder => _ = builder.UseExceptionMapping(),
		"circuit-breaker" => builder => _ = builder.UseCircuitBreaker(),
		_ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown middleware"),
	};

	[Theory]
	[InlineData("retry")]
	[InlineData("exception-mapping")]
	[InlineData("circuit-breaker")]
	public async Task ThrowThroughMiddlewareThatConvertsExceptionsToResults(string middleware)
	{
		// The middleware that exist to turn an exception into an outcome are exactly the ones that can undo this. Each of these
		// catches Exception broadly and returns a failed result, so a pipeline carrying any of them would hand the caller back the
		// 500-shaped result the throw was introduced to replace -- and a caller mapping failure to 400 blames the end user again.
		// Retrying is also wasted work: a registration that is missing on the first attempt is missing on the fifth.
		await using var provider = BuildWith(PipelineNamed(middleware));
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(new OrphanAction(), TestContext.Current.CancellationToken));

		ShouldNameTheTypeAndBothRemedies(exception);
	}

	[Theory]
	[InlineData("exception-mapping")]
	public async Task StillConvertAThrowingHandlerUnderTheSameMiddleware(string middleware)
	{
		// The liveness half of the arm above. Letting the configuration fault through must not stop the middleware doing its job:
		// a registered handler that throws is a runtime outcome and must still arrive as a failed result. A fix that simply stopped
		// this middleware catching anything would satisfy the safety arm perfectly and break every consumer of it.
		//
		// Only exception mapping belongs here. Turning a fault into an outcome is the whole of what it does, so a fault it lets
		// through is a fault it failed to handle. Retry and the circuit breaker do something else with a fault -- count attempts,
		// count failures -- and then let it through, so the equivalent liveness for them is that the counting still happens:
		// NotRetryTheMissingRegistration / StillRetryATransientFaultUnderTheSameConfiguration below for retry, and the
		// threshold arms in the circuit breaker's own tests for the breaker.
		await using var provider = BuildWith(PipelineNamed(middleware));
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new BoomAction(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeFalse();
	}
	private static ServiceProvider BuildWithRetryCounting(AttemptCounter counter, int maxAttempts)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(counter);

		// Through the builder's own seam, not GetServices<IDispatchMiddleware>(): that enumerable is the legacy path
		// and the real pipeline is built from the registry, so a middleware registered only in DI never runs.
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.UseRetry();
			_ = dispatch.UseMiddleware<AttemptCountingMiddleware>();
		});
		_ = services.Configure<RetryOptions>(options =>
		{
			options.MaxAttempts = maxAttempts;

			// The arms count attempts rather than waiting on them, so a real backoff would only make them slow.
			options.BaseDelay = TimeSpan.Zero;
			options.MaxDelay = TimeSpan.Zero;

			// Load-bearing, and the reason the count below is worth asserting. The missing-handler exception derives from
			// InvalidOperationException, which ships in this floor, so under the defaults retry already declines to repeat it --
			// for a reason that has nothing to do with it being a configuration fault, and that a consumer may edit, since the
			// set is mutable. Leaving the floor in place would let an arm asserting a single attempt pass against a pipeline
			// with no pass-through at all. Cleared, only the dedicated pass-through can hold the count at one.
			options.NonRetryableExceptions.Clear();
		});
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task NotRetryTheMissingRegistration()
	{
		// Surfacing the fault is only half of it. A registration absent on the first attempt is absent on the last, so retrying
		// spends the caller's latency budget to reach the same conclusion, and on a pipeline that also breaks a circuit it spends
		// the failure budget too. The outcome is identical either way, so this counts what retry actually invoked.
		var counter = new AttemptCounter { FailWithRetryableFault = false };
		await using var provider = BuildWithRetryCounting(counter, maxAttempts: 3);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => dispatcher.DispatchAsync(new OrphanAction(), TestContext.Current.CancellationToken));

		ShouldNameTheTypeAndBothRemedies(exception);
		counter.Attempts.ShouldBe(1, "a missing registration must reach the host on the first attempt, not after three");
	}

	[Fact]
	public async Task StillRetryATransientFaultUnderTheSameConfiguration()
	{
		// Liveness for the arm above, and what makes its count mean something: same container, same counter, same message type,
		// one variable changed -- the fault is now one retry exists to absorb. Without this, "one attempt" would be satisfied by
		// a pipeline that had stopped retrying anything at all.
		var counter = new AttemptCounter { FailWithRetryableFault = true };
		await using var provider = BuildWithRetryCounting(counter, maxAttempts: 3);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Exhausting every attempt rethrows the original fault, so the count is what separates this arm from the
		// one above -- both faults reach the caller as themselves, and only the number of attempts differs.
		_ = await Should.ThrowAsync<TimeoutException>(
			() => dispatcher.DispatchAsync(new OrphanAction(), TestContext.Current.CancellationToken));

		counter.Attempts.ShouldBe(3, "a transient fault must still be retried to the configured cap");
	}
}
