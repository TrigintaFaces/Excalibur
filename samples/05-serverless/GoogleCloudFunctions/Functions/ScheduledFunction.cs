// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Google.Cloud.Functions.Framework;

using GoogleCloudFunctionsSample.Messages;

using Microsoft.Extensions.Logging;

namespace GoogleCloudFunctionsSample.Functions;

/// <summary>
/// Cloud Scheduler-triggered Google Cloud Function for periodic tasks.
/// Demonstrates Dispatch messaging integration with Cloud Scheduler.
/// </summary>
/// <remarks>
/// Cloud Scheduler triggers functions via Pub/Sub or HTTP. This example
/// uses the CloudEvent approach for Pub/Sub-based scheduler triggers.
///
/// To set up a Cloud Scheduler job:
/// <code>
/// gcloud scheduler jobs create pubsub daily-report \
///   --schedule="0 9 * * *" \
///   --topic=scheduled-tasks \
///   --message-body='{"taskName":"DailyReport"}' \
///   --location=us-central1
/// </code>
/// </remarks>
public class ScheduledFunction : ICloudEventFunction
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<ScheduledFunction> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScheduledFunction"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public ScheduledFunction(IDispatcher dispatcher, ILogger<ScheduledFunction> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Handles scheduled task invocations from Cloud Scheduler.
	/// </summary>
	/// <param name="cloudEvent">The CloudEvent from Cloud Scheduler via Pub/Sub.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task HandleAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(cloudEvent);

		_logger.LogInformation(
			"Scheduled trigger: {Type}, Time: {Time}",
			cloudEvent.Type,
			cloudEvent.Time);

		// Parse scheduler payload
		var taskName = ExtractTaskName(cloudEvent.Data);

		// Create scheduled task event
		var scheduledEvent = new ScheduledTaskEvent(
			TaskId: $"TASK-{Guid.NewGuid():N}",
			TaskName: taskName,
			ExecutedAt: DateTimeOffset.UtcNow);

		// Create dispatch context
		var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

		// Dispatch the event
		_ = await _dispatcher.DispatchAsync(scheduledEvent, dispatchContext, cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Scheduled task {TaskName} completed at {ExecutedAt}",
			taskName,
			scheduledEvent.ExecutedAt);
	}

	private static string ExtractTaskName(object? data)
	{
		if (data is JsonElement jsonElement)
		{
			// Try to extract from Pub/Sub message structure
			if (jsonElement.TryGetProperty("message", out var message) &&
				message.TryGetProperty("data", out var base64Data))
			{
				try
				{
					var decodedBytes = Convert.FromBase64String(base64Data.GetString() ?? string.Empty);
					var decodedJson = System.Text.Encoding.UTF8.GetString(decodedBytes);
					using var doc = JsonDocument.Parse(decodedJson);
					if (doc.RootElement.TryGetProperty("taskName", out var taskNameProp))
					{
						return taskNameProp.GetString() ?? "UnknownTask";
					}
				}
				catch (FormatException)
				{
					// Not base64 encoded, try direct parsing
				}
			}

			// Direct property access
			if (jsonElement.TryGetProperty("taskName", out var directTaskName))
			{
				return directTaskName.GetString() ?? "UnknownTask";
			}
		}

		return "ScheduledTask";
	}
}
