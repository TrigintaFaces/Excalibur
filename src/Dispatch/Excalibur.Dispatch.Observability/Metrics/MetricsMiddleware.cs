// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Middleware that records metrics for message processing using OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// This middleware records the following metrics:
/// <list type="bullet">
/// <item><c>dispatch.messages.processed</c> - Counter of processed messages</item>
/// <item><c>dispatch.messages.duration</c> - Histogram of processing duration in milliseconds</item>
/// <item><c>dispatch.messages.failed</c> - Counter of failed messages</item>
/// </list>
/// </para>
/// <para>
/// Metric recording is cross-cutting instrumentation and fails open: if a metric operation
/// throws, the failure is logged and the real handler result (or the genuine handler exception)
/// is preserved unchanged, matching the Microsoft skip-pattern for optional infrastructure.
/// </para>
/// <para>
/// To export metrics, add the Excalibur.Dispatch meter to your OpenTelemetry configuration:
/// <code>
/// services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics.AddDispatchMetrics());
/// </code>
/// </para>
/// </remarks>
/// <param name="metrics">The dispatch metrics instance.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[AppliesTo(MessageKinds.All)]
internal sealed partial class MetricsMiddleware(IDispatchMetrics metrics, ILogger<MetricsMiddleware> logger)
	: IDispatchMiddleware
{
	private readonly IDispatchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
	private readonly ILogger<MetricsMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		var messageType = message.GetType().Name;
		var handlerType = context.GetItem<Type>("HandlerType")?.Name ?? "Unknown";

		var stopwatch = ValueStopwatch.StartNew();

		// The handler call is OUTSIDE any instrumentation try/catch: a genuine handler exception
		// propagates unchanged. Metric recording (below) fails open and never alters the outcome.
		IMessageResult result;
		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			RecordFailureSafe(messageType, ex.GetType().Name, stopwatch);
			throw;
		}

		RecordSuccessSafe(result, messageType, handlerType, stopwatch);
		return result;
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Fail-open instrumentation: a metric failure must not lose or alter the real handler result (Microsoft skip-pattern). Cancellation is rethrown.")]
	private void RecordSuccessSafe(IMessageResult result, string messageType, string handlerType, ValueStopwatch stopwatch)
	{
		try
		{
			var success = result.IsSuccess;
			_metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, messageType, success);
			_metrics.RecordMessageProcessed(messageType, handlerType);

			if (!success && result.ProblemDetails is { } pd)
			{
				_metrics.RecordMessageFailed(messageType, pd.Type ?? "unknown", retryAttempt: 0);
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// Fail open: a metric failure must not lose or alter the real handler result.
			LogMetricsInstrumentationFailed(ex);
		}
	}

	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Fail-open instrumentation: a metric failure must not mask the genuine handler exception being propagated (Microsoft skip-pattern).")]
	private void RecordFailureSafe(string messageType, string errorType, ValueStopwatch stopwatch)
	{
		try
		{
			_metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, messageType, success: false);
			_metrics.RecordMessageFailed(messageType, errorType, retryAttempt: 0);
		}
		catch (Exception ex)
		{
			// Fail open: a metric failure must not mask the genuine handler exception being propagated.
			LogMetricsInstrumentationFailed(ex);
		}
	}

	[LoggerMessage(ObservabilityEventId.MetricsInstrumentationFailed, LogLevel.Warning,
		"Metrics instrumentation failed and was skipped; dispatch outcome preserved")]
	private partial void LogMetricsInstrumentationFailed(Exception ex);
}
