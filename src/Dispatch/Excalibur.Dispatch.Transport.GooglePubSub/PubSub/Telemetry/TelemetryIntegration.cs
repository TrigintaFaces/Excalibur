// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Provides telemetry integration hooks for Google Pub/Sub components.
/// </summary>
public static class TelemetryIntegration
{
	/// <summary>
	/// Creates a telemetry-enabled message handler wrapper.
	/// </summary>
	/// <param name="innerHandler"> The inner message handler. </param>
	/// <param name="telemetryProvider"> The telemetry provider. </param>
	/// <param name="subscription"> The subscription name. </param>
	/// <returns> A wrapped handler with telemetry. </returns>
	public static Func<PubsubMessage, CancellationToken, Task> WrapWithTelemetry(
		Func<PubsubMessage, CancellationToken, Task> innerHandler,
		PubSubTelemetryProvider telemetryProvider,
		string subscription) =>
		async (message, cancellationToken) =>
		{
			var stopwatch = Stopwatch.StartNew();
			using var activity = telemetryProvider.RecordMessageReceived(message, subscription);

			try
			{
				await innerHandler(message, cancellationToken).ConfigureAwait(false);

				telemetryProvider.RecordMessageAcknowledged(
					message.MessageId,
					subscription,
					stopwatch.Elapsed);
			}
			catch (Exception ex)
			{
				telemetryProvider.RecordMessageNacked(
					message.MessageId,
					subscription,
					ex.GetType().Name);

				_ = (activity?.SetTag("exception.type", ex.GetType().FullName));
				_ = (activity?.SetTag("exception.message", ex.Message));
				_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
				throw;
			}
		};

	/// <summary>
	/// Creates a telemetry exporter background service.
	/// </summary>
	/// <param name="telemetryProvider"> The telemetry provider. </param>
	/// <param name="logger"> Logger instance. </param>
	/// <param name="exportInterval"> Export interval. </param>
	/// <param name="cancellationToken"> The cancellation token to stop the exporter. </param>
	/// <returns> A task that exports telemetry periodically. </returns>
	public static async Task RunTelemetryExporterAsync(
		PubSubTelemetryProvider telemetryProvider,
		ILogger logger,
		TimeSpan exportInterval,
		CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(exportInterval);

		logger.LogInformation(
			"Started telemetry exporter with interval {Interval}",
			exportInterval);

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				_ = await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
				await telemetryProvider.ExportToCloudMonitoringAsync(cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error exporting telemetry");
			}
		}

		logger.LogInformation("Telemetry exporter stopped");
	}

	/// <summary>
	/// Enriches an activity with Pub/Sub specific tags.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="message"> The Pub/Sub message. </param>
	/// <param name="subscription"> The subscription name. </param>
	public static void EnrichActivity(Activity activity, PubsubMessage message, string subscription)
	{
		if (activity == null)
		{
			return;
		}

		_ = activity.SetTag("messaging.system", "pubsub");
		_ = activity.SetTag("messaging.destination", subscription);
		_ = activity.SetTag("messaging.message_id", message.MessageId);

		if (!string.IsNullOrEmpty(message.OrderingKey))
		{
			_ = activity.SetTag("messaging.pubsub.ordering_key", message.OrderingKey);
		}

		if (message.PublishTime != null)
		{
			_ = activity.SetTag("messaging.pubsub.publish_time", message.PublishTime.ToDateTime());
		}

		// Add message attributes as tags (with prefix to avoid conflicts)
		foreach (var attribute in message.Attributes)
		{
			_ = activity.SetTag($"messaging.pubsub.attribute.{attribute.Key}", attribute.Value);
		}
	}

	/// <summary>
	/// Injects trace context into a message for propagation.
	/// </summary>
	/// <param name="message"> The message to inject context into. </param>
	public static void InjectTraceContext(PubsubMessage message)
	{
		var activity = Activity.Current;
		if (activity != null && activity.Context.TraceId != default)
		{
			if (activity.Id != null)
			{
				message.Attributes["traceparent"] = activity.Id;
			}

			if (!string.IsNullOrEmpty(activity.TraceStateString))
			{
				message.Attributes["tracestate"] = activity.TraceStateString;
			}
		}
	}
}
