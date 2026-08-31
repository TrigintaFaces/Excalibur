// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // ValueTask in FakeItEasy .Returns()

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Validation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// The retry middleware decides how many attempts a fault gets. It does not decide what the fault is, so
/// neither of its terminal paths may restate somebody else's fault as an outcome of its own: a fault it
/// declines to retry propagates, an exhausted one rethrows the original, and an exhausted stream of failed
/// results returns the downstream's own last result.
/// </summary>
/// <remarks>
/// <para>
/// The middleware used to convert both terminals into problem details of its own. The consumer's exception
/// mapper and typed exception handler match on the consumer's exception type, so a substituted problem type
/// meant neither ever saw the fault they were registered for — the same defect the terminal dispatch
/// handler had one layer lower, moved up a stage.
/// </para>
/// <para>
/// The dead-letter decorator composed that substituted problem type by string, so removing it without
/// re-composing the decorator would have turned auto-dead-lettering off invisibly. It now reads the
/// exhaustion the retry middleware records on the context, which is correct whether the exhaustion arrives
/// as a result or as an exception — and which of those a consumer sees depends on registration order,
/// because exception mapping and the decorator share a stage.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
public sealed class RetryExhaustionPropagationShould
{
	private const string MappedProblemType = "urn:test:mapped-by-the-consumers-mapper";
	private const string DownstreamProblemType = "urn:test:the-downstreams-own-failure";
	private const string RecoveredMarker = "recovered-by-the-consumers-typed-handler";
	private const string PermanentFault = "the consumer's handler rejected this input";
	private const string TransientFault = "the consumer's handler timed out";
	private const string ExhaustedCounter = "dispatch.retry.exhausted";
	private const string RetryMeterName = "Excalibur.Dispatch.RetryMiddleware";

	private sealed record ProbeAction : IDispatchAction;

	/// <summary>Counts real invocations, so a retried permanent fault shows up as a repeated side effect.</summary>
	private sealed class InvocationCounter
	{
		private int _count;

		public int Count => Volatile.Read(ref _count);

		public void Increment() => _ = Interlocked.Increment(ref _count);
	}

	/// <summary>Supplies the fault a run is exercising, so one handler serves every arm.</summary>
	private sealed class FaultSpec(Func<Exception> create)
	{
		public Exception Create() => create();
	}

	private sealed class FaultingHandler(InvocationCounter counter, FaultSpec fault) : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken)
		{
			counter.Increment();
			throw fault.Create();
		}
	}

	private sealed class NeverInvokedHandler : IActionHandler<ProbeAction>
	{
		public Task HandleAsync(ProbeAction action, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	/// <summary>
	/// Returns a transient (503) failure from the <see cref="DispatchMiddlewareStage.End"/> stage, which sorts
	/// below the error-handling stage — so the retry middleware sees a repeatedly failing downstream rather
	/// than a throwing one, which is the other exhaustion path.
	/// </summary>
	private sealed class TransientlyFailingMiddleware(InvocationCounter counter) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			counter.Increment();
			return ValueTask.FromResult<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
			{
				Type = DownstreamProblemType,
				Title = "The downstream's own failure",
				ErrorCode = 503,
				Status = 503,
				Detail = "service unavailable",
				Instance = context.MessageId ?? string.Empty,
			}));
		}
	}

	/// <summary>Converts the fault into a success carrying its type and message, which no other stage can do.</summary>
	private sealed class RecoveringHandler : ITypedExceptionHandler<TimeoutException>
	{
		public ValueTask<ExceptionHandlerResult> HandleAsync(
			TimeoutException exception,
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(ExceptionHandlerResult.Handled(
				MessageResult.Success($"{RecoveredMarker}|{exception.GetType().Name}|{exception.Message}")));
	}

	// ── ARM 1 — DECLINED ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// A fault the retry middleware declines to retry reaches the consumer's mapper. The explicit
	/// <c>ShouldNotBe</c> is what makes this non-vacuous: the middleware's own former problem type was also a
	/// failed result, so asserting failure alone passed before the fix.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersMapperAFaultRetryDeclinedToRetry()
	{
		await using var provider = BuildDefaults(() => new ArgumentException(PermanentFault), out var counter);

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldNotBe(
			"RetryError",
			"declining to retry is a statement about attempts, not about the fault");
		result.ProblemDetails.Type.ShouldBe(MappedProblemType);
		result.ProblemDetails.Detail.ShouldBe(PermanentFault);
		counter.Count.ShouldBe(1, "a permanent fault is attempted once");
	}

	// ── ARM 2 — EXHAUSTED BY EXCEPTION ───────────────────────────────────────────────────────────────

	/// <summary>
	/// A transient fault exhausted to the cap reaches the consumer's mapper too, and reaches it UNWRAPPED.
	/// The mapped detail carries the handler's own message, so re-introducing any wrapper exception — which
	/// would be mapped by its own type, not the consumer's — reddens this arm.
	/// </summary>
	[Fact]
	public async Task GiveTheConsumersMapperTheOriginalFaultAfterExhaustingEveryAttempt()
	{
		await using var provider = BuildDefaults(() => new TimeoutException(TransientFault), out var counter, maxAttempts: 2);

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		counter.Count.ShouldBe(2, "a transient fault is attempted up to the configured cap");
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(MappedProblemType);
		result.ProblemDetails.Detail.ShouldBe(
			TransientFault,
			"the consumer's mapper must receive their own exception, not a retry-exhausted wrapper around it");
	}

	// ── ARM 3 — EXHAUSTED BY RESULT ──────────────────────────────────────────────────────────────────

	/// <summary>
	/// Exhaustion by repeatedly returned failures has no exception to raise, so the downstream's own last
	/// result is what the caller sees. A middleware substituting its own would discard what the pipeline
	/// below deliberately produced.
	/// </summary>
	[Fact]
	public async Task ReturnTheDownstreamsOwnFailureAfterExhaustingEveryAttempt()
	{
		var counter = new InvocationCounter();
		var services = BaseServices(counter, () => new TimeoutException(TransientFault), maxAttempts: 3);
		_ = services.AddSingleton(new TransientlyFailingMiddleware(counter));
		_ = services.AddDispatch(dispatch => _ = dispatch.ConfigurePipeline(
			"default",
			pipeline => _ = pipeline.UseDefaults().Use<TransientlyFailingMiddleware>()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, NeverInvokedHandler>();

		await using var provider = services.BuildServiceProvider();

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		counter.Count.ShouldBe(3, "a transient 503 is retried to the cap");
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(
			DownstreamProblemType,
			"the caller must see the failure the pipeline below produced, not one the retry middleware invented");
		result.ProblemDetails.Type.ShouldNotBe("RetryExhausted");
	}

	// ── ARM 4 — THE DEAD-LETTER RE-COMPOSITION ───────────────────────────────────────────────────────

	/// <summary>
	/// Auto-dead-lettering still fires on a genuine exhaustion, and the exception still reaches the mapper
	/// registered outside the decorator. Both halves matter: routing that swallowed the fault would satisfy
	/// the enqueue count while breaking every mapping above it.
	/// </summary>
	[Fact]
	public async Task DeadLetterAnExhaustedDispatchExactlyOnce_AndStillLetTheFaultReachTheMapper()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		await using var provider = BuildWithDeadLetterQueue(dlq, () => new TimeoutException(TransientFault));

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, DeadLetterReason.MaxRetriesExceeded, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustHaveHappenedOnceExactly();

		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(
			MappedProblemType,
			"the decorator rethrows after routing, so the mapper above it still sees the consumer's exception");
	}

	/// <summary>
	/// SAFETY — a fault retry declined to retry is not an exhaustion, so it is not dead-lettered. Without
	/// this arm a decorator routing every propagating fault satisfies the liveness arm above.
	/// </summary>
	[Fact]
	public async Task NotDeadLetterAFaultRetryDeclinedToRetry()
	{
		var dlq = A.Fake<IDeadLetterQueue>();
		await using var provider = BuildWithDeadLetterQueue(dlq, () => new ArgumentException(PermanentFault));

		_ = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		A.CallTo(() => dlq.EnqueueAsync<IDispatchMessage>(
				A<IDispatchMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
				A<Exception?>._, A<IDictionary<string, string>?>._))
			.MustNotHaveHappened();
	}

	// ── ARM 5 — THE EXHAUSTION COUNTER ───────────────────────────────────────────────────────────────

	/// <summary>
	/// The exhaustion counter emits exactly once on BOTH exhaustion sub-paths. The exception sub-path is the
	/// load-bearing half: the terminal leaves it by rethrowing, so a counter placed after the rethrow is
	/// unreachable and this arm goes RED while every result-shaped assertion still passes.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task EmitTheExhaustionCounterExactlyOnceOnBothExhaustionPaths(bool exhaustByException)
	{
		var recorded = 0L;
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (instrument.Meter.Name == RetryMeterName && instrument.Name == ExhaustedCounter)
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
		{
			// Isolate to THIS test's dispatches via the message.type tag: the meter is process-static, so a
			// concurrent emitter in another assembly would otherwise contaminate the count.
			foreach (var tag in tags)
			{
				if (tag.Key == "message.type" && (tag.Value as string) == nameof(ProbeAction))
				{
					_ = Interlocked.Add(ref recorded, value);
					return;
				}
			}
		});
		listener.Start();

		await using var provider = exhaustByException
			? BuildDefaults(() => new TimeoutException(TransientFault), out _, maxAttempts: 2)
			: BuildExhaustingByResult();

		_ = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		Interlocked.Read(ref recorded).ShouldBe(1, "every exhaustion counts once, and counts before the terminal leaves");
	}

	// ── ARM 6 — REGISTRATION ORDER ───────────────────────────────────────────────────────────────────

	/// <summary>
	/// The typed exception handler middleware shares the retry middleware's stage, so their relative order is
	/// the order they were registered in. The consumer's handler must fire in both positions: outside retry
	/// it sees the rethrown fault, inside it sees the fault before retry ever classifies it. The outer
	/// position is the one that could not work while the middleware converted its terminals to results.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task InvokeTheConsumersTypedHandlerWhicheverSideOfRetryItIsRegistered(bool typedHandlerOutsideRetry)
	{
		var counter = new InvocationCounter();
		var services = BaseServices(counter, () => new TimeoutException(TransientFault), maxAttempts: 2);
		services.TryAddSingleton<TypedExceptionHandlerMiddleware>();
		_ = services.AddSingleton<ITypedExceptionHandler<TimeoutException>, RecoveringHandler>();
		_ = services.AddDispatch(dispatch => _ = dispatch.ConfigurePipeline(
			"default",
			pipeline => _ = typedHandlerOutsideRetry
				? pipeline.Use<TypedExceptionHandlerMiddleware>().Use<RetryMiddleware>()
				: pipeline.Use<RetryMiddleware>().Use<TypedExceptionHandlerMiddleware>()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, FaultingHandler>();

		await using var provider = services.BuildServiceProvider();

		var result = await provider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new ProbeAction(), TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			"only the consumer's typed handler can turn this fault into a success, so a success proves it ran");
		var sentinel = (result as IMessageResult<string>)?.ReturnValue;
		sentinel.ShouldBe(
			$"{RecoveredMarker}|{nameof(TimeoutException)}|{TransientFault}",
			"the handler's own exception must arrive unwrapped, with its type and message intact");
	}

	// ── wiring ───────────────────────────────────────────────────────────────────────────────────────

	private static ServiceProvider BuildDefaults(
		Func<Exception> fault,
		out InvocationCounter counter,
		int maxAttempts = 3)
	{
		var invocations = new InvocationCounter();
		counter = invocations;

		var services = BaseServices(invocations, fault, maxAttempts);
		_ = services.AddDispatch(dispatch => _ = dispatch.ConfigurePipeline("default", pipeline => _ = pipeline.UseDefaults()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, FaultingHandler>();

		return services.BuildServiceProvider();
	}

	private static ServiceProvider BuildExhaustingByResult()
	{
		var counter = new InvocationCounter();
		var services = BaseServices(counter, () => new TimeoutException(TransientFault), maxAttempts: 2);
		_ = services.AddSingleton(new TransientlyFailingMiddleware(counter));
		_ = services.AddDispatch(dispatch => _ = dispatch.ConfigurePipeline(
			"default",
			pipeline => _ = pipeline.UseDefaults().Use<TransientlyFailingMiddleware>()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, NeverInvokedHandler>();

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Exception mapping is registered BEFORE the dead-letter decorator. They share the post-processing
	/// stage, so registration order decides which is outer: the mapper ends up outside, and the decorator
	/// therefore has to let the fault through rather than convert it.
	/// </summary>
	private static ServiceProvider BuildWithDeadLetterQueue(IDeadLetterQueue dlq, Func<Exception> fault)
	{
		var services = BaseServices(new InvocationCounter(), fault, maxAttempts: 2);
		_ = services.AddSingleton(dlq);
		_ = services.AddDeadLetterOnExhaustion();
		_ = services.AddDispatch(dispatch => _ = dispatch.ConfigurePipeline(
			"default",
			pipeline => _ = pipeline
				.Use<ExceptionMappingMiddleware>()
				.Use<DeadLetterOnExhaustionMiddleware>()
				.Use<RetryMiddleware>()));
		_ = services.AddTransient<IActionHandler<ProbeAction>, FaultingHandler>();

		return services.BuildServiceProvider();
	}

	private static IServiceCollection BaseServices(InvocationCounter counter, Func<Exception> fault, int maxAttempts)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(counter);
		_ = services.AddSingleton(new FaultSpec(fault));
		_ = services.Configure<RetryOptions>(options =>
		{
			options.MaxAttempts = maxAttempts;
			options.BaseDelay = TimeSpan.FromMilliseconds(1);
		});
		_ = services.AddExceptionMapping(mapping => _ = mapping
			.Map<ArgumentException>(ex => new MessageProblemDetails
			{
				Type = MappedProblemType,
				Title = "Mapped by the consumer",
				Status = 422,
				Detail = ex.Message,
			})
			.Map<TimeoutException>(ex => new MessageProblemDetails
			{
				Type = MappedProblemType,
				Title = "Mapped by the consumer",
				Status = 504,
				Detail = ex.Message,
			}));
		services.TryAddSingleton<IValidatorResolver, NoOpValidatorResolver>();
		services.TryAddSingleton<IMessageValidationService, NoOpValidationService>();
		services.TryAddSingleton<ValidationMiddleware>();
		services.TryAddSingleton<LoggingMiddleware>();
		services.TryAddSingleton<TimeoutMiddleware>();
		services.TryAddSingleton<RetryMiddleware>();
		services.TryAddSingleton<ExceptionMappingMiddleware>();

		return services;
	}
}
