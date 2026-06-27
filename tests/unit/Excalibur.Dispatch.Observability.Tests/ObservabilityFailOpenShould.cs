// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Observability.Sampling;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// FR-B2 regression lock (bead lxixkx): cross-cutting observability MUST fail OPEN — a failure in an
/// instrumentation/observability dependency must never break the core dispatch (the handler still runs
/// and its real result is returned) — WHILE the two deliberate opt-in consumer halt-policies are
/// PRESERVED (fail CLOSED) when explicitly enabled.
/// </summary>
/// <remarks>
/// <para>
/// This is an author≠implementer lock. The fail-open seam under test lives in
/// <see cref="ContextObservabilityMiddleware"/> (the instrumentation try/catch swallows
/// <c>EnrichActivity</c> / <c>RecordContextState</c> / metric failures, while the handler call sits
/// OUTSIDE every swallow), in <see cref="MetricsMiddleware"/> (metric recording fails open, handler
/// outcome preserved), and in <see cref="TraceSamplerMiddleware"/> (a sampler failure leaves tracing at
/// its default and dispatch continues). The two deliberate opt-in fail-closed policies are
/// <c>ContextObservabilityOptions.FailOnIntegrityViolation</c> → <see cref="ContextIntegrityException"/>
/// and <c>ContextObservabilityOptions.Limits.FailOnSizeThresholdExceeded</c> →
/// <see cref="ContextSizeExceededException"/> — a deliberate consumer halt is NOT an instrumentation
/// "failure" and MUST still throw.
/// </para>
/// <para>
/// Non-vacuity: against a pre-fix implementation where instrumentation calls are unguarded, the
/// fail-open cases would propagate the thrown instrumentation exception out of <c>InvokeAsync</c> and
/// break the dispatch (RED). Symmetrically, an implementation that swallowed the opt-in policy
/// exceptions would let the fail-closed cases return normally (RED). The full production RED-proof
/// (mutating the impl) is deferred to post-commit because the middleware source is reserved by the
/// implementing lane; this lock binds both halves of the contract against the committed surface.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "FailOpen")]
public sealed class ObservabilityFailOpenShould
{
	// ---------------------------------------------------------------------------------------------
	// (a) FAIL-OPEN: a throwing instrumentation dependency must NOT break dispatch — the inner
	//     handler still runs and its real result is returned unchanged.
	// ---------------------------------------------------------------------------------------------

#pragma warning disable IL2026, IL3050
	[Fact]
	public async Task ContextObservability_FailOpen_WhenTraceEnricherThrows()
	{
		// Arrange — the trace-enricher (instrumentation) throws.
		var tracker = A.Fake<IContextFlowTracker>();
		var metrics = A.Fake<IContextFlowMetrics>();
		var enricher = A.Fake<IContextTraceEnricher>();
		A.CallTo(() => tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);
		A.CallTo(() => enricher.EnrichActivity(A<Activity?>._, A<IMessageContext>._))
			.Throws(new InvalidOperationException("instrumentation boom"));

		using var middleware = CreateContextMiddleware(new ContextObservabilityOptions { Enabled = true }, tracker, metrics, enricher);
		var expected = A.Fake<IMessageResult>();

		// Act — dispatch still completes and returns the real handler result.
		var actual = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(expected), CancellationToken.None);

		// Assert
		actual.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task ContextObservability_FailOpen_WhenTrackerRecordThrows()
	{
		// Arrange — the context-flow tracker (instrumentation) throws while recording state.
		var tracker = A.Fake<IContextFlowTracker>();
		var metrics = A.Fake<IContextFlowMetrics>();
		var enricher = A.Fake<IContextTraceEnricher>();
		A.CallTo(() => tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);
		A.CallTo(() => tracker.RecordContextState(A<IMessageContext>._, A<string>._, A<IReadOnlyDictionary<string, object>?>._))
			.Throws(new InvalidOperationException("instrumentation boom"));

		using var middleware = CreateContextMiddleware(new ContextObservabilityOptions { Enabled = true }, tracker, metrics, enricher);
		var expected = A.Fake<IMessageResult>();

		// Act
		var actual = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(expected), CancellationToken.None);

		// Assert
		actual.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task ContextObservability_FailOpen_WhenMetricsThrows()
	{
		// Arrange — the metrics collector (instrumentation) throws on the success path.
		var tracker = A.Fake<IContextFlowTracker>();
		var metrics = A.Fake<IContextFlowMetrics>();
		var enricher = A.Fake<IContextTraceEnricher>();
		A.CallTo(() => tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);
		A.CallTo(() => metrics.RecordContextPreservationSuccess(A<string>._))
			.Throws(new InvalidOperationException("instrumentation boom"));

		using var middleware = CreateContextMiddleware(new ContextObservabilityOptions { Enabled = true }, tracker, metrics, enricher);
		var expected = A.Fake<IMessageResult>();

		// Act
		var actual = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(expected), CancellationToken.None);

		// Assert
		actual.ShouldBeSameAs(expected);
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public async Task Metrics_FailOpen_WhenMetricRecordingThrows()
	{
		// Arrange — IDispatchMetrics (instrumentation) throws; the handler outcome must be preserved.
		var metrics = A.Fake<IDispatchMetrics>();
		A.CallTo(() => metrics.RecordProcessingDuration(A<double>._, A<string>._, A<bool>._))
			.Throws(new InvalidOperationException("instrumentation boom"));

		var middleware = new MetricsMiddleware(metrics, NullLogger<MetricsMiddleware>.Instance);
		var expected = A.Fake<IMessageResult>();
		A.CallTo(() => expected.IsSuccess).Returns(true);

		// Act
		var actual = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(expected), CancellationToken.None);

		// Assert
		actual.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task TraceSampler_FailOpen_WhenSamplerThrows()
	{
		// Arrange — the trace sampler (instrumentation) throws; dispatch continues unsuppressed.
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._))
			.Throws(new InvalidOperationException("instrumentation boom"));

		var middleware = new TraceSamplerMiddleware(sampler, NullLogger<TraceSamplerMiddleware>.Instance);
		var expected = A.Fake<IMessageResult>();

		// Act
		var actual = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(expected), CancellationToken.None);

		// Assert
		actual.ShouldBeSameAs(expected);
	}

	// ---------------------------------------------------------------------------------------------
	// (b) FAIL-CLOSED PRESERVED: the two deliberate opt-in consumer halt-policies STILL throw when
	//     explicitly enabled — a deliberate consumer halt is not an instrumentation "failure".
	// ---------------------------------------------------------------------------------------------

#pragma warning disable IL2026, IL3050
	[Fact]
	public async Task ContextObservability_StillThrows_WhenFailOnIntegrityViolationEnabled()
	{
		// Arrange — opt-in policy #1: FailOnIntegrityViolation -> ContextIntegrityException.
		var tracker = A.Fake<IContextFlowTracker>();
		var metrics = A.Fake<IContextFlowMetrics>();
		var enricher = A.Fake<IContextTraceEnricher>();
		var options = new ContextObservabilityOptions
		{
			Enabled = true,
			ValidateContextIntegrity = true,
			FailOnIntegrityViolation = true,
		};
		A.CallTo(() => tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(false);

		using var middleware = CreateContextMiddleware(options, tracker, metrics, enricher);

		// Act & Assert — the deliberate halt-policy is preserved (not swallowed by fail-open).
		await Should.ThrowAsync<ContextIntegrityException>(() =>
			middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(A.Fake<IMessageResult>()), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ContextObservability_StillThrows_WhenFailOnSizeThresholdExceededEnabled()
	{
		// Arrange — opt-in policy #2: Limits.FailOnSizeThresholdExceeded -> ContextSizeExceededException.
		var tracker = A.Fake<IContextFlowTracker>();
		var metrics = A.Fake<IContextFlowMetrics>();
		var enricher = A.Fake<IContextTraceEnricher>();
		var options = new ContextObservabilityOptions { Enabled = true };
		options.Limits.MaxContextSizeBytes = 1; // force the captured snapshot to exceed the threshold
		options.Limits.FailOnSizeThresholdExceeded = true;
		A.CallTo(() => tracker.ValidateContextIntegrity(A<IMessageContext>._)).Returns(true);

		using var middleware = CreateContextMiddleware(options, tracker, metrics, enricher);

		// Act & Assert — the deliberate halt-policy is preserved (not swallowed by fail-open).
		await Should.ThrowAsync<ContextSizeExceededException>(() =>
			middleware.InvokeAsync(A.Fake<IDispatchMessage>(), CreateFakeContext(), HandlerReturning(A.Fake<IMessageResult>()), CancellationToken.None).AsTask());
	}
#pragma warning restore IL2026, IL3050

	// ---------------------------------------------------------------------------------------------
	// Helpers
	// ---------------------------------------------------------------------------------------------

	private static ContextObservabilityMiddleware CreateContextMiddleware(
		ContextObservabilityOptions options,
		IContextFlowTracker tracker,
		IContextFlowMetrics metrics,
		IContextTraceEnricher enricher) =>
		new(
			NullLogger<ContextObservabilityMiddleware>.Instance,
			tracker,
			metrics,
			enricher,
			MsOptions.Create(options));

	private static DispatchRequestDelegate HandlerReturning(IMessageResult result) =>
		(_, _, _) => ValueTask.FromResult(result);

	private static IMessageContext CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		var features = new Dictionary<Type, object>();

		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.Features).Returns(features);

		context.SetMessageType("TestMessage");

		return context;
	}
}
