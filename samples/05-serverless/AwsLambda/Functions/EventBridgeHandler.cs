// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.Core;

using AwsLambdaSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AwsLambdaSample.Functions;

/// <summary>
/// AWS Lambda function handling EventBridge scheduled events.
/// Demonstrates Dispatch messaging integration with EventBridge (CloudWatch Events) triggers.
/// </summary>
public class EventBridgeHandler
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<EventBridgeHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventBridgeHandler"/> class.
	/// </summary>
	public EventBridgeHandler()
	{
		_serviceProvider = Startup.ServiceProvider;
		_logger = _serviceProvider.GetRequiredService<ILogger<EventBridgeHandler>>();
	}

	/// <summary>
	/// Handles scheduled EventBridge events for daily report generation.
	/// </summary>
	/// <param name="scheduledEvent">The EventBridge scheduled event.</param>
	/// <param name="context">The Lambda execution context.</param>
	/// <remarks>
	/// <para>
	/// This function is triggered by EventBridge rules on a schedule.
	/// Example rule: cron(0 0 * * ? *) - daily at midnight UTC
	/// </para>
	/// <para>
	/// EventBridge provides more flexibility than CloudWatch Events:
	/// - Cross-account event delivery
	/// - Archive and replay capabilities
	/// - Schema registry integration
	/// </para>
	/// </remarks>
	[LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
	public async Task GenerateDailyReportAsync(
		ScheduledEvent scheduledEvent,
		ILambdaContext context)
	{
		_logger.LogInformation(
			"EventBridge: Scheduled event triggered at {Time}, RequestId: {RequestId}",
			scheduledEvent.Time,
			context.AwsRequestId);

		try
		{
			// Create scheduled task event
			var taskEvent = new ScheduledTaskEvent(
				$"TASK-{DateTime.UtcNow:yyyyMMddHHmmss}",
				"DailyReportGeneration",
				DateTimeOffset.UtcNow);

			// Get dispatcher from DI
			using var scope = _serviceProvider.CreateScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

			// Create dispatch context
			var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event
			_ = await dispatcher.DispatchAsync(taskEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);

			_logger.LogInformation(
				"Daily report generation initiated, TaskId: {TaskId}",
				taskEvent.TaskId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate daily report");
			throw; // Rethrow to mark invocation as failed
		}
	}

	/// <summary>
	/// Handles scheduled EventBridge events for hourly health checks.
	/// </summary>
	/// <param name="scheduledEvent">The EventBridge scheduled event.</param>
	/// <param name="context">The Lambda execution context.</param>
	[LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
	public async Task HourlyHealthCheckAsync(
		ScheduledEvent scheduledEvent,
		ILambdaContext context)
	{
		_logger.LogInformation(
			"EventBridge: Health check triggered at {Time}, " +
			"RemainingTime: {RemainingTime}ms",
			scheduledEvent.Time,
			context.RemainingTime.TotalMilliseconds);

		// Create health check task event
		var taskEvent = new ScheduledTaskEvent(
			$"HEALTH-{DateTime.UtcNow:yyyyMMddHHmmss}",
			"HourlyHealthCheck",
			DateTimeOffset.UtcNow);

		// Get dispatcher from DI
		using var scope = _serviceProvider.CreateScope();
		var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

		// Create dispatch context
		var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

		// Dispatch the event
		_ = await dispatcher.DispatchAsync(taskEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);

		_logger.LogInformation("Health check completed successfully");
	}
}
