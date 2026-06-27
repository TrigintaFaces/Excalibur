// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // ValueTask in FakeItEasy .Returns()

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Author≠impl regression lock for S851 Lane 5 · <c>l7m7nr</c> (+ S852 MS-C · <c>qu3182</c>/<c>jj9gon</c>) —
/// <see cref="RetryMiddleware"/> must emit retry metrics (it emitted none before <c>l7m7nr</c>):
/// <c>dispatch.retry.attempts</c> on each retry and <c>dispatch.retry.exhausted</c> when retries are
/// exhausted — <b>on BOTH exhaustion code paths</b> (transient-failed-result AND retryable-exception).
/// </summary>
/// <remarks>
/// Authored independently of the implementer (BackendDeveloper) against the committed seam. The
/// jj9gon/qu3182 (S852) restructure converges both exhaustion paths on the SINGLE reachable post-loop
/// <c>RetryExhausted</c> terminal (<c>RetryMiddleware.cs:212-241</c>), which emits the exhausted counter
/// exactly once. Before it, the retryable-EXCEPTION path returned <c>RetryError</c> and emitted nothing —
/// the qu3182 undercount.
/// A <see cref="MeterListener"/> captures the <c>Excalibur.Dispatch.RetryMiddleware</c> meter.
/// <b>RED mutants:</b> remove <c>RetryAttemptsCounter.Add</c> ⇒ attempts fact RED; remove
/// <c>RetryExhaustionsCounter.Add</c> ⇒ both exhausted facts RED; revert the retryable-exception path to
/// return <c>RetryError</c> instead of breaking to the terminal ⇒ the EXCEPTION-exhaustion fact RED
/// (the qu3182 regression).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RetryMiddlewareMetricsShould
{
	private const string RetryMeterName = "Excalibur.Dispatch.RetryMiddleware";
	private const string AttemptsCounter = "dispatch.retry.attempts";
	private const string ExhaustedCounter = "dispatch.retry.exhausted";

	[Fact]
	public async Task RecordRetryAttempts_OnEachRetry()
	{
		// Transient exception on every attempt ⇒ the retryable-exception path runs and increments attempts.
		var recorded = await CaptureAsync(
			(_, _, _) => throw new TimeoutException("transient"),
			new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) });

		Sum(recorded, AttemptsCounter).ShouldBeGreaterThan(0, "the retry-attempts counter must emit on each retry");
	}

	[Fact]
	public async Task RecordRetryExhausted_OnTerminalFailure()
	{
		// A persistently transient FAILED RESULT (RFC7807 503) is retried until exhausted, then abandoned
		// on the last attempt — the reachable terminal path that emits dispatch.retry.exhausted.
		var transientFailure = A.Fake<IMessageResult>();
		_ = A.CallTo(() => transientFailure.IsSuccess).Returns(false);
		_ = A.CallTo(() => transientFailure.Succeeded).Returns(false);
		_ = A.CallTo(() => transientFailure.ProblemDetails).Returns(new MessageProblemDetails { Status = 503 });

		var recorded = await CaptureAsync(
			(_, _, _) => new ValueTask<IMessageResult>(transientFailure),
			new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) });

		Sum(recorded, ExhaustedCounter).ShouldBe(1, "the exhausted counter must emit once on terminal failure (failed-result path)");
	}

	[Fact]
	public async Task RecordRetryExhausted_OnRetryableExceptionExhaustion()
	{
		// qu3182 (the dual-code-path other half): a retryable EXCEPTION (TimeoutException → classifier
		// Transient) thrown on every attempt is retried to the cap, then converges on the SAME single
		// post-loop RetryExhausted terminal — which emits dispatch.retry.exhausted exactly once. Before the
		// restructure this path returned RetryError and emitted NOTHING (the undercount). RED mutant:
		// revert the retryable-exception-at-cap path to return RetryError ⇒ exhausted == 0 here ⇒ RED.
		var recorded = await CaptureAsync(
			(_, _, _) => throw new TimeoutException("transient — never recovers"),
			new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) });

		Sum(recorded, ExhaustedCounter).ShouldBe(1, "the exhausted counter must ALSO emit once on the exception-exhaustion path (qu3182 no-undercount)");
	}

	private static long Sum(IEnumerable<(string Name, long Value)> recorded, string counter) =>
		recorded.Where(m => m.Name == counter).Sum(m => m.Value);

	private static async Task<List<(string Name, long Value)>> CaptureAsync(DispatchRequestDelegate next, RetryOptions options)
	{
		var recorded = new List<(string Name, long Value)>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, l) =>
		{
			if (instrument.Meter.Name == RetryMeterName)
			{
				l.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => recorded.Add((instrument.Name, value)));
		listener.Start();

		_ = await CreateSut(options).InvokeAsync(
			A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);

		return recorded;
	}

	private static RetryMiddleware CreateSut(RetryOptions options)
	{
		var sanitizer = A.Fake<ITelemetrySanitizer>();
		_ = A.CallTo(() => sanitizer.SanitizeTag(A<string>._, A<string?>._)).ReturnsLazily(c => c.GetArgument<string?>(1));

		var logger = A.Fake<ILogger<RetryMiddleware>>();
		_ = A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		return new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(options), sanitizer, logger);
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("retry-metric-msg");
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}
}
