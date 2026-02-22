// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Services;

/// <summary>
/// Background service that polls for due saga timeouts and delivers them to saga handlers.
/// </summary>
/// <remarks>
/// <para>
/// This service periodically checks <see cref="ISagaTimeoutStore.GetDueTimeoutsAsync"/> for timeouts
/// that are ready for delivery, deserializes the timeout message, and dispatches it through
/// <see cref="IDispatcher"/> where saga handling middleware routes it to the correct saga instance.
/// </para>
/// <para>
/// <b>Reliability:</b> Timeouts are marked as delivered only after successful dispatch, ensuring
/// at-least-once delivery semantics. The underlying <see cref="ISagaTimeoutStore"/> implementation
/// (e.g., SqlServerSagaTimeoutStore) must persist timeouts to survive process restarts.
/// </para>
/// </remarks>
public partial class SagaTimeoutDeliveryService : BackgroundService
{
	private readonly ISagaTimeoutStore _timeoutStore;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SagaTimeoutDeliveryService> _logger;
	private readonly SagaTimeoutOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaTimeoutDeliveryService"/> class.
	/// </summary>
	/// <param name="timeoutStore">The timeout store to poll for due timeouts.</param>
	/// <param name="serviceProvider">The service provider for creating scoped dispatchers.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The timeout delivery options.</param>
	public SagaTimeoutDeliveryService(
		ISagaTimeoutStore timeoutStore,
		IServiceProvider serviceProvider,
		ILogger<SagaTimeoutDeliveryService> logger,
		IOptions<SagaTimeoutOptions> options)
	{
		_timeoutStore = timeoutStore ?? throw new ArgumentNullException(nameof(timeoutStore));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Saga timeout types are preserved through registration")]
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Saga timeout types are preserved through registration")]
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var activity = SagaActivitySource.StartActivity("SagaTimeoutDeliveryService.Execute");

		LogServiceStarting();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessDueTimeoutsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Graceful shutdown
				break;
			}
			catch (Exception ex)
			{
				LogPollCycleFailed(ex);
			}

			try
			{
				await Task.Delay(_options.PollInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogServiceStopping();
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization may require runtime code generation")]
	private async Task ProcessDueTimeoutsAsync(CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("ProcessDueTimeouts");

		var dueTimeouts = await _timeoutStore
			.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, cancellationToken)
			.ConfigureAwait(false);

		if (dueTimeouts.Count == 0)
		{
			return;
		}

		_ = (activity?.SetTag("timeout.count", dueTimeouts.Count));

		if (_options.EnableVerboseLogging)
		{
			LogProcessingTimeouts(dueTimeouts.Count);
		}

		// Process up to batch size
		var batch = dueTimeouts.Take(_options.BatchSize);

		foreach (var timeout in batch)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			await DeliverTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
		}
	}

	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON deserialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON deserialization may require runtime code generation")]
	private async Task DeliverTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("DeliverTimeout");
		_ = (activity?.SetTag("saga.id", timeout.SagaId));
		_ = (activity?.SetTag("timeout.id", timeout.TimeoutId));
		_ = (activity?.SetTag("timeout.type", timeout.TimeoutType));
		_ = (activity?.SetTag("timeout.due_at", timeout.DueAt.ToString("O")));

		try
		{
			// Deserialize timeout message
			var timeoutType = ResolveTypeByName(timeout.TimeoutType);
			if (timeoutType is null)
			{
				LogTimeoutTypeResolutionFailed(
					timeout.TimeoutType,
					timeout.TimeoutId);
				// Mark as delivered to prevent retry loop for unresolvable types
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			object? timeoutMessage;
			if (timeout.TimeoutData is not null)
			{
				timeoutMessage = JsonSerializer.Deserialize(timeout.TimeoutData, timeoutType);
			}
			else
			{
				timeoutMessage = CreateTimeoutMessageInstance(timeoutType);
			}

			if (timeoutMessage is null)
			{
				LogTimeoutMessageCreationFailed(timeout.TimeoutType);
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			if (timeoutMessage is not IDispatchMessage dispatchMessage)
			{
				LogTimeoutMessageTypeInvalid(timeout.TimeoutType);
				await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
				return;
			}

			// Dispatch via saga handling infrastructure using scoped dispatcher
			using var scope = _serviceProvider.CreateScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

			var context = new MessageContext(dispatchMessage, scope.ServiceProvider)
			{
				MessageId = timeout.TimeoutId,
				MessageType = timeout.TimeoutType,
				ReceivedTimestampUtc = DateTimeOffset.UtcNow,
			};

			_ = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);

			// Mark delivered after successful dispatch
			await _timeoutStore.MarkDeliveredAsync(timeout.TimeoutId, cancellationToken).ConfigureAwait(false);

			if (_options.EnableVerboseLogging)
			{
				LogTimeoutDelivered(timeout.TimeoutId, timeout.SagaId);
			}
		}
		catch (Exception ex)
		{
			LogTimeoutDeliveryFailed(timeout.TimeoutId, timeout.SagaId, ex);
			_ = (activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message));
			// Do NOT mark as delivered - will retry on next poll
		}
	}

	private static Type? ResolveTypeByName(string typeName)
	{
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		var assemblySeparator = typeName.IndexOf(',', StringComparison.Ordinal);
		if (assemblySeparator <= 0)
		{
			return null;
		}

		var simpleTypeName = typeName[..assemblySeparator];
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			var resolved = assembly.GetType(simpleTypeName, throwOnError: false, ignoreCase: false);
			if (resolved != null)
			{
				return resolved;
			}
		}

		return null;
	}

	private static object? CreateTimeoutMessageInstance(Type timeoutType)
	{
		var constructor = timeoutType.GetConstructor(Type.EmptyTypes);
		return constructor?.Invoke(null);
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.TimeoutDeliveryStarted, LogLevel.Information,
		"Saga timeout delivery service starting")]
	private partial void LogServiceStarting();

	[LoggerMessage(SagaEventId.TimeoutServiceStopped, LogLevel.Information,
		"Saga timeout delivery service stopping")]
	private partial void LogServiceStopping();

	[LoggerMessage(SagaEventId.TimeoutProcessingStarted, LogLevel.Debug,
		"Processing {Count} due timeouts")]
	private partial void LogProcessingTimeouts(int count);

	[LoggerMessage(SagaEventId.TimeoutDeliveredSuccessfully, LogLevel.Debug,
		"Delivered timeout {TimeoutId} to saga {SagaId}")]
	private partial void LogTimeoutDelivered(string timeoutId, string sagaId);

	[LoggerMessage(SagaEventId.TimeoutDeliveryFailed, LogLevel.Error,
		"Failed to deliver timeout {TimeoutId} to saga {SagaId}")]
	private partial void LogTimeoutDeliveryFailed(string timeoutId, string sagaId, Exception ex);

	[LoggerMessage(SagaEventId.TimeoutBatchCompleted, LogLevel.Warning,
		"Timeout poll cycle failed, will retry next cycle")]
	private partial void LogPollCycleFailed(Exception ex);

	[LoggerMessage(SagaEventId.TimeoutTypeResolutionFailed, LogLevel.Warning,
		"Could not resolve timeout type {TimeoutType} for timeout {TimeoutId}")]
	private partial void LogTimeoutTypeResolutionFailed(string timeoutType, string timeoutId);

	[LoggerMessage(SagaEventId.TimeoutMessageCreationFailed, LogLevel.Warning,
		"Could not create timeout message instance for type {TimeoutType}")]
	private partial void LogTimeoutMessageCreationFailed(string timeoutType);

	[LoggerMessage(SagaEventId.TimeoutMessageTypeInvalid, LogLevel.Warning,
		"Timeout message type {TimeoutType} does not implement IDispatchMessage")]
	private partial void LogTimeoutMessageTypeInvalid(string timeoutType);
}
