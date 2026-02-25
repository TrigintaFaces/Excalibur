// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Cronos;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Time-aware scheduled message service that integrates TimePolicy for configurable timeout handling during scheduled message execution.
/// R7.4: Configurable timeout handling with adaptive capabilities for scheduled message processing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="TimeAwareScheduledMessageService" /> class. </remarks>
/// <param name="scheduleStore"> Persistent store for schedule management and retrieval operations. </param>
/// <param name="dispatcher"> Message dispatcher for executing scheduled message deliveries. </param>
/// <param name="serializer"> JSON serializer for message deserialization and processing. </param>
/// <param name="timePolicy"> Time policy for configurable timeout handling. </param>
/// <param name="timeoutMonitor"> Optional timeout monitor for adaptive timeout management. </param>
/// <param name="options"> Configuration options for service behavior and performance tuning. </param>
/// <param name="logger"> Logger for service operations, errors, and performance monitoring. </param>
public partial class TimeAwareScheduledMessageService(
	IScheduleStore scheduleStore,
	IDispatcher dispatcher,
	IJsonSerializer serializer,
	ITimePolicy timePolicy,
	ITimeoutMonitor? timeoutMonitor,
	IOptions<SchedulerOptions> options,
	ILogger<TimeAwareScheduledMessageService> logger) : BackgroundService
{
	private readonly TimeSpan _pollInterval = options.Value.PollInterval;

	/// <summary>
	/// Stops the time-aware scheduled message service with graceful shutdown, ensuring proper resource cleanup and pending operation completion.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token for coordinated shutdown timing. </param>
	/// <returns> Task representing the asynchronous service shutdown operation. </returns>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		if (scheduleStore is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Executes the time-aware background scheduled message processing loop with timeout management and adaptive timeout capabilities. This
	/// method applies configurable timeouts to all scheduling operations including message retrieval, deserialization, and dispatch.
	/// </summary>
	/// <param name="stoppingToken"> Cancellation token that triggers when the service should stop processing. </param>
	/// <returns> Task representing the long-running scheduled message processing operation. </returns>
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Scheduled message processing relies on runtime type resolution and serializer configuration.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Scheduled message processing uses runtime deserialization; AOT users should opt out of scheduling features.")]
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogServiceStarted(logger);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Apply timeout to schedule retrieval operation
				using var retrievalToken = CreateTimeoutToken(TimeoutOperationType.Database, stoppingToken);
				var schedules = await scheduleStore.GetAllAsync(retrievalToken.Token).ConfigureAwait(false);

				foreach (var item in schedules)
				{
					if (!item.Enabled || item.NextExecutionUtc is null || item.NextExecutionUtc > DateTimeOffset.UtcNow)
					{
						continue;
					}

					await ProcessScheduledMessageAsync((ScheduledMessage)item, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				LogServiceStopping(logger);
				break;
			}
			catch (TimeoutException ex)
			{
				LogTimeoutDuringProcessing(logger, ex);
			}
			catch (Exception ex)
			{
				LogErrorProcessingMessages(logger, ex);
			}

			await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
		}

		LogServiceStopped(logger);
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.ScheduledServiceStarting, LogLevel.Information,
		"Time-aware scheduled message service started")]
	private static partial void LogServiceStarted(ILogger logger);

	[LoggerMessage(DeliveryEventId.ScheduledServiceStopping, LogLevel.Information,
		"Time-aware scheduled message service stopping")]
	private static partial void LogServiceStopping(ILogger logger);

	[LoggerMessage(DeliveryEventId.ScheduledServiceStopped, LogLevel.Information,
		"Time-aware scheduled message service stopped")]
	private static partial void LogServiceStopped(ILogger logger);

	[LoggerMessage(DeliveryEventId.ScheduledTimeoutDuringProcessing, LogLevel.Warning,
		"Timeout occurred during scheduled message processing")]
	private static partial void LogTimeoutDuringProcessing(ILogger logger, Exception ex);

	[LoggerMessage(DeliveryEventId.ScheduledProcessingError, LogLevel.Error,
		"Error processing scheduled messages")]
	private static partial void LogErrorProcessingMessages(ILogger logger, Exception ex);

	[LoggerMessage(DeliveryEventId.ScheduledUnknownMessageType, LogLevel.Warning,
		"Unknown scheduled message type {MessageName} for item {ItemId}")]
	private static partial void LogUnknownMessageType(ILogger logger, string messageName, string itemId);

	[LoggerMessage(DeliveryEventId.ScheduledDeserializationFailed, LogLevel.Warning,
		"Failed to deserialize scheduled message {ItemId}")]
	private static partial void LogDeserializationFailed(ILogger logger, string itemId);

	[LoggerMessage(DeliveryEventId.ScheduledMessageProcessed, LogLevel.Debug,
		"Successfully processed scheduled message {ItemId} of type {MessageType}")]
	private static partial void LogMessageProcessed(ILogger logger, string itemId, string messageType);

	[LoggerMessage(DeliveryEventId.ScheduledTimeoutProcessingMessage, LogLevel.Warning,
		"Timeout occurred processing scheduled message {ItemId}")]
	private static partial void LogTimeoutProcessingMessage(ILogger logger, Exception ex, string itemId);

	[LoggerMessage(DeliveryEventId.ScheduledErrorProcessingMessage, LogLevel.Error,
		"Error processing scheduled message {ItemId}")]
	private static partial void LogErrorProcessingMessage(ILogger logger, Exception ex, string itemId);

	[LoggerMessage(DeliveryEventId.ScheduledUnknownDispatchType, LogLevel.Warning,
		"Unknown message type {MessageType} for scheduled dispatch")]
	private static partial void LogUnknownDispatchType(ILogger logger, string messageType);

	/// <summary>
	/// Resolves the message type from the message name with timeout protection.
	/// </summary>
	/// <param name="messageName"> The fully qualified message type name. </param>
	/// <param name="cancellationToken"> Cancellation token for timeout enforcement. </param>
	/// <returns> The resolved message type or null if not found. </returns>
	private static Task<Type?> ResolveMessageTypeAsync(string messageName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_ = MessageTypeRegistry.TryGetType(messageName, out var type);
		return Task.FromResult<Type?>(type);
	}

	/// <summary>
	/// Creates a dispatch context with timeout-aware metadata.
	/// </summary>
	/// <param name="item"> The scheduled message. </param>
	/// <param name="timeoutContext"> The timeout context for correlation. </param>
	/// <returns> A configured dispatch context. </returns>
	private static MessageContext CreateDispatchContext(ScheduledMessage item, TimeoutContext timeoutContext)
	{
		var context = DispatchContextInitializer.CreateDefaultContext();
		context.CorrelationId = item.CorrelationId;
		context.TraceParent = item.TraceParent;
		context.TenantId = item.TenantId;
		context.UserId = item.UserId;

		// Add timeout context information to the dispatch context properties
		context.Properties["TimeoutOperationType"] = timeoutContext.Complexity.ToString();
		context.Properties["ScheduledMessageId"] = item.Id.ToString();
		context.Properties["OriginalScheduleTime"] = item.NextExecutionUtc?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";

		return context;
	}

	/// <summary>
	/// Determines the operation complexity based on message characteristics.
	/// </summary>
	/// <param name="item"> The scheduled message to analyze. </param>
	/// <returns> The determined operation complexity level. </returns>
	private static OperationComplexity DetermineOperationComplexity(ScheduledMessage item)
	{
		// Analyze message characteristics to determine complexity
		var messageSize = item.MessageBody?.Length ?? 0;
		var hasCronExpression = !string.IsNullOrWhiteSpace(item.CronExpression);
		var hasTimeZone = !string.IsNullOrWhiteSpace(item.TimeZoneId);

		return (messageSize, hasCronExpression, hasTimeZone) switch
		{
			( > 10000, true, true) => OperationComplexity.Heavy,
			( > 5000, true, _) or ( > 10000, _, _) => OperationComplexity.Complex,
			( > 1000, _, _) or (_, true, _) => OperationComplexity.Normal,
			_ => OperationComplexity.Simple,
		};
	}

	/// <summary>
	/// Gets the message type from the message name for timeout context.
	/// </summary>
	/// <param name="messageName"> The fully qualified message type name. </param>
	/// <returns> The message type or null if not resolvable. </returns>
	private static Type? GetMessageType(string messageName)
	{
		_ = MessageTypeRegistry.TryGetType(messageName, out var type);
		return type;
	}

	/// <summary>
	/// Processes a single scheduled message with comprehensive timeout management and monitoring.
	/// </summary>
	/// <param name="item"> The scheduled message to process. </param>
	/// <param name="stoppingToken"> Cancellation token for operation coordination. </param>
	/// <returns> Task representing the scheduled message processing operation. </returns>
	[RequiresUnreferencedCode(
			"Calls Excalibur.Dispatch.Delivery.Scheduling.TimeAwareScheduledMessageService.DeserializeMessageAsync(String, Type, CancellationToken)")]
	[RequiresDynamicCode(
			"Calls Excalibur.Dispatch.Delivery.Scheduling.TimeAwareScheduledMessageService.DeserializeMessageAsync(String, Type, CancellationToken)")]
	private async Task ProcessScheduledMessageAsync(ScheduledMessage item, CancellationToken stoppingToken)
	{
		ITimeoutOperationToken? operationToken = null;

		try
		{
			// Start timeout monitoring for the entire operation
			var timeoutContext = new TimeoutContext
			{
				MessageType = GetMessageType(item.MessageName),
				Complexity = DetermineOperationComplexity(item),
				IsRetry = false,
				RetryCount = 0,
			};

			operationToken = timeoutMonitor?.StartOperation(TimeoutOperationType.Scheduling, timeoutContext);

			// Apply timeout to message type resolution
			using var typeResolutionToken = CreateTimeoutToken(TimeoutOperationType.Validation, stoppingToken);
			var type = await ResolveMessageTypeAsync(item.MessageName, typeResolutionToken.Token).ConfigureAwait(false);

			if (type is null)
			{
				LogUnknownMessageType(logger, item.MessageName, item.Id.ToString());
				timeoutMonitor?.CompleteOperation(operationToken, success: false, timedOut: false);
				return;
			}

			// Apply timeout to message deserialization
			using var deserializationToken = CreateTimeoutToken(TimeoutOperationType.Serialization, stoppingToken);
			var message = await DeserializeMessageAsync(item.MessageBody, type, deserializationToken.Token).ConfigureAwait(false);

			if (message is null)
			{
				LogDeserializationFailed(logger, item.Id.ToString());
				timeoutMonitor?.CompleteOperation(operationToken, success: false, timedOut: false);
				return;
			}

			// Create dispatch context with timeout considerations
			var context = CreateDispatchContext(item, timeoutContext);

			// Apply timeout to message dispatch
			using var dispatchToken = CreateTimeoutToken(TimeoutOperationType.Handler, stoppingToken, timeoutContext);
			await DispatchMessageAsync(message, context, dispatchToken.Token).ConfigureAwait(false);

			// Update schedule for next execution
			await UpdateScheduleAsync(item, stoppingToken).ConfigureAwait(false);

			timeoutMonitor?.CompleteOperation(operationToken, success: true, timedOut: false);
			LogMessageProcessed(logger, item.Id.ToString(), item.MessageName);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			timeoutMonitor?.CompleteOperation(operationToken, success: false, timedOut: false);
			throw;
		}
		catch (TimeoutException ex)
		{
			timeoutMonitor?.CompleteOperation(operationToken, success: false, timedOut: true);
			LogTimeoutProcessingMessage(logger, ex, item.Id.ToString());
		}
		catch (Exception ex)
		{
			timeoutMonitor?.CompleteOperation(operationToken, success: false, timedOut: false);
			LogErrorProcessingMessage(logger, ex, item.Id.ToString());
		}
	}

	/// <summary>
	/// Creates a timeout-aware cancellation token for the specified operation type.
	/// </summary>
	/// <param name="operationType"> The type of operation for timeout calculation. </param>
	/// <param name="parentToken"> The parent cancellation token. </param>
	/// <param name="context"> Optional timeout context for adaptive timeout calculation. </param>
	/// <returns> A cancellation token source that respects timeout policies. </returns>
	private CancellationTokenSource CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken,
		TimeoutContext? context = null)
	{
		if (!timePolicy.ShouldApplyTimeout(operationType, context))
		{
			return CancellationTokenSource.CreateLinkedTokenSource(parentToken);
		}

		var timeout = timePolicy.GetTimeoutFor(operationType);

		var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
		cts.CancelAfter(timeout);

		return cts;
	}

	/// <summary>
	/// Deserializes the message with timeout protection.
	/// </summary>
	/// <param name="messageBody"> The serialized message body. </param>
	/// <param name="messageType"> The target message type. </param>
	/// <param name="cancellationToken"> Cancellation token for timeout enforcement. </param>
	/// <returns> The deserialized message or null if deserialization fails. </returns>
	/// <exception cref="TimeoutException"> </exception>
	[RequiresUnreferencedCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer.DeserializeAsync(String, Type)")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task<object?> DeserializeMessageAsync(string messageBody, Type messageType, CancellationToken cancellationToken)
	{
		try
		{
			return await serializer.DeserializeAsync(messageBody, messageType).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException($"Message deserialization timed out for type {messageType.Name}");
		}
	}

	/// <summary>
	/// Dispatches the message with timeout protection.
	/// </summary>
	/// <param name="message"> The message to dispatch. </param>
	/// <param name="context"> The dispatch context. </param>
	/// <param name="cancellationToken"> Cancellation token for timeout enforcement. </param>
	/// <returns> Task representing the dispatch operation. </returns>
	/// <exception cref="TimeoutException"> </exception>
	private async Task DispatchMessageAsync(object message, IMessageContext context, CancellationToken cancellationToken)
	{
		try
		{
			switch (message)
			{
				case IDispatchAction action:
					_ = await dispatcher.DispatchAsync(action, context, cancellationToken).ConfigureAwait(false);
					break;

				case IDispatchEvent evt:
					_ = await dispatcher.DispatchAsync(evt, context, cancellationToken).ConfigureAwait(false);
					break;

				default:
					LogUnknownDispatchType(logger, message.GetType().Name);
					break;
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw new TimeoutException($"Message dispatch timed out for type {message.GetType().Name}");
		}
	}

	/// <summary>
	/// Updates the schedule for next execution with timeout protection.
	/// </summary>
	/// <param name="item"> The scheduled message to update. </param>
	/// <param name="stoppingToken"> Cancellation token for operation coordination. </param>
	/// <returns> Task representing the schedule update operation. </returns>
	private async Task UpdateScheduleAsync(ScheduledMessage item, CancellationToken stoppingToken)
	{
		using var updateToken = CreateTimeoutToken(TimeoutOperationType.Database, stoppingToken);

		// Calculate next execution time
		if (!string.IsNullOrWhiteSpace(item.CronExpression))
		{
			var cron = CronExpression.Parse(item.CronExpression);
			var timeZone = !string.IsNullOrWhiteSpace(item.TimeZoneId)
				? TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId)
				: TimeZoneInfo.Utc;

			item.NextExecutionUtc = cron.GetNextOccurrence(DateTimeOffset.UtcNow, timeZone);
		}
		else if (item.Interval is not null)
		{
			item.NextExecutionUtc = DateTimeOffset.UtcNow.Add(item.Interval.Value);
		}
		else
		{
			// One-time execution completed
			item.Enabled = false;
		}

		// Update last execution time
		item.LastExecutionUtc = DateTimeOffset.UtcNow;

		// Store the updated schedule
		await scheduleStore.StoreAsync(item, updateToken.Token).ConfigureAwait(false);
	}
}
