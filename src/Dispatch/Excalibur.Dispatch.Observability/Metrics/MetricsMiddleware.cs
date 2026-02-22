// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Delivery;

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
/// To export metrics, add the Excalibur.Dispatch meter to your OpenTelemetry configuration:
/// <code>
/// services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics.AddDispatchMetrics());
/// </code>
/// </para>
/// </remarks>
/// <param name="metrics">The dispatch metrics instance.</param>
[AppliesTo(MessageKinds.All)]
public sealed class MetricsMiddleware(IDispatchMetrics metrics) : IDispatchMiddleware
{
	private readonly IDispatchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

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
		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			var success = result.IsSuccess;
			_metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, messageType, success);
			_metrics.RecordMessageProcessed(messageType, handlerType);

			if (!success && result.ProblemDetails is { } pd)
			{
				_metrics.RecordMessageFailed(messageType, pd.Type ?? "unknown", retryAttempt: 0);
			}

			return result;
		}
		catch (Exception ex)
		{
			_metrics.RecordProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, messageType, success: false);
			_metrics.RecordMessageFailed(messageType, ex.GetType().Name, retryAttempt: 0);
			throw;
		}
	}
}
