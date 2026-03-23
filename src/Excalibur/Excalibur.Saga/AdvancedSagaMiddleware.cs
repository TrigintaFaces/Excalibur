// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Idempotency;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga;

/// <summary>
/// Middleware for handling advanced saga orchestration in the dispatch pipeline.
/// </summary>
/// <remarks>
/// This middleware intercepts saga-related messages and coordinates their execution
/// through the saga orchestrator, supporting conditional steps, parallel execution,
/// and automatic compensation on failure.
/// </remarks>
public sealed partial class AdvancedSagaMiddleware : IDispatchMiddleware
{
	private const int MaxCacheEntries = 1024;

	private readonly ISagaOrchestrator _orchestrator;
	private readonly ISagaStateStore _stateStore;
	private readonly ISagaIdempotencyProvider? _idempotencyProvider;
	private readonly AdvancedSagaOptions _options;
	private readonly ILogger<AdvancedSagaMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdvancedSagaMiddleware"/> class.
	/// </summary>
	/// <param name="orchestrator">The saga orchestrator.</param>
	/// <param name="stateStore">The saga state store.</param>
	/// <param name="options">The saga options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="idempotencyProvider">Optional idempotency provider for compensation deduplication.</param>
	public AdvancedSagaMiddleware(
		ISagaOrchestrator orchestrator,
		ISagaStateStore stateStore,
		IOptions<AdvancedSagaOptions> options,
		ILogger<AdvancedSagaMiddleware> logger,
		ISagaIdempotencyProvider? idempotencyProvider = null)
	{
		_orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_idempotencyProvider = idempotencyProvider;
	}

	/// <inheritdoc/>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

	/// <inheritdoc/>
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

	/// <inheritdoc/>
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Check if this message is saga-related
		if (!IsSagaMessage(message, context))
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var sagaId = GetSagaId(context);

		try
		{
			LogProcessingSagaMessage(_logger, message.GetType().Name, sagaId);

			// Check for existing saga state
			if (!string.IsNullOrEmpty(sagaId))
			{
				var existingState = await _stateStore.GetStateAsync(sagaId, cancellationToken).ConfigureAwait(false);

				if (existingState != null)
				{
					// Update context with saga information
					context.SetItem("SagaState", existingState);
					context.SetItem("SagaId", sagaId);
				}
			}

			// Execute the handler
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			// Post-execution saga state management
			if (result.IsSuccess && _options.EnableStatePersistence)
			{
				await HandleSagaCompletionAsync(context, cancellationToken).ConfigureAwait(false);
			}
			else if (!result.IsSuccess && _options.EnableAutoCompensation)
			{
				await HandleSagaCompensationAsync(sagaId, cancellationToken).ConfigureAwait(false);
			}

			return result;
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			LogSagaCancelled(_logger, sagaId);

			if (!string.IsNullOrEmpty(sagaId))
			{
				await _orchestrator.CancelSagaAsync(sagaId, "Operation cancelled", cancellationToken).ConfigureAwait(false);
			}

			throw;
		}
		catch (Exception ex)
		{
			LogSagaProcessingError(_logger, sagaId, ex);

			if (!string.IsNullOrEmpty(sagaId) && _options.EnableAutoCompensation)
			{
				await HandleSagaCompensationAsync(sagaId, cancellationToken).ConfigureAwait(false);
			}

			return MessageResult.Failed(ex.Message);
		}
	}

	/// <summary>
	/// Tracks compensation operations that are in flight within this process to prevent the
	/// TOCTOU race between <see cref="ISagaIdempotencyProvider.IsProcessedAsync"/> and
	/// <see cref="ISagaIdempotencyProvider.MarkProcessedAsync"/>. The external provider
	/// handles cross-process deduplication; this dictionary handles in-process atomicity.
	/// </summary>
	private readonly ConcurrentDictionary<string, byte> _compensationInFlight = new();

	/// <summary>
	/// Caches whether a message type has the <see cref="SagaMessageAttribute"/> to avoid repeated reflection.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, bool> SagaMessageAttributeCache = new();

	private static bool IsSagaMessage(IDispatchMessage message, IMessageContext context)
	{
		// Check if the message or context indicates saga involvement
		var isSaga = context.GetItem<bool?>("IsSagaMessage");
		if (isSaga == true)
		{
			return true;
		}

		// Check for saga-related attributes or markers (cached to avoid per-call reflection)
		var messageType = message.GetType();

		if (SagaMessageAttributeCache.TryGetValue(messageType, out var cached))
		{
			return cached;
		}

		var result = messageType.GetCustomAttributes(typeof(SagaMessageAttribute), true).Length > 0;

		if (SagaMessageAttributeCache.Count < MaxCacheEntries)
		{
			SagaMessageAttributeCache.TryAdd(messageType, result);
		}

		return result;
	}

	private static string? GetSagaId(IMessageContext context)
	{
		var sagaId = context.GetItem<string>("SagaId");
		if (!string.IsNullOrEmpty(sagaId))
		{
			return sagaId;
		}

		return context.CorrelationId;
	}

	[LoggerMessage(LogLevel.Debug,
		"Processing saga message {MessageType} for saga {SagaId}")]
	private static partial void LogProcessingSagaMessage(
		ILogger logger,
		string messageType,
		string? sagaId);

	[LoggerMessage(LogLevel.Warning, "Saga {SagaId} was cancelled")]
	private static partial void LogSagaCancelled(
		ILogger logger,
		string? sagaId);

	[LoggerMessage(LogLevel.Error, "Error processing saga {SagaId}")]
	private static partial void LogSagaProcessingError(
		ILogger logger,
		string? sagaId,
		Exception ex);

	[LoggerMessage(LogLevel.Information,
		"Saga {SagaId} step completed successfully")]
	private static partial void LogSagaStepCompleted(
		ILogger logger,
		string sagaId);

	[LoggerMessage(LogLevel.Warning,
		"Initiating compensation for saga {SagaId}")]
	private static partial void LogSagaCompensationStarting(
		ILogger logger,
		string sagaId);

	[LoggerMessage(LogLevel.Information,
		"Saga {SagaId} compensation already processed -- skipping duplicate")]
	private static partial void LogSagaCompensationAlreadyProcessed(
		ILogger logger,
		string sagaId);

	[LoggerMessage(LogLevel.Error, "Failed to compensate saga {SagaId}")]
	private static partial void LogSagaCompensationFailed(
		ILogger logger,
		string sagaId,
		Exception ex);

	private Task HandleSagaCompletionAsync(IMessageContext context, CancellationToken cancellationToken)
	{
		_ = cancellationToken; // Future use for async operations

		var sagaId = context.GetItem<string>("SagaId");
		if (!string.IsNullOrEmpty(sagaId))
		{
			LogSagaStepCompleted(_logger, sagaId);

			// Mark saga step as completed in observability
			// Actual state management is handled by the saga orchestrator
		}

		return Task.CompletedTask;
	}

	private async Task HandleSagaCompensationAsync(string? sagaId, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(sagaId))
		{
			return;
		}

		var compensationKey = $"{sagaId}:compensate";

		// Atomic in-process claim: TryAdd returns false if another thread already claimed this saga.
		// This eliminates the TOCTOU window between IsProcessedAsync and MarkProcessedAsync.
		if (!_compensationInFlight.TryAdd(compensationKey, 0))
		{
			LogSagaCompensationAlreadyProcessed(_logger, sagaId);
			return;
		}

		try
		{
			// Check external idempotency store for cross-process deduplication
			if (_idempotencyProvider != null)
			{
				var alreadyProcessed = await _idempotencyProvider.IsProcessedAsync(sagaId, compensationKey, cancellationToken).ConfigureAwait(false);
				if (alreadyProcessed)
				{
					LogSagaCompensationAlreadyProcessed(_logger, sagaId);
					return;
				}
			}

			LogSagaCompensationStarting(_logger, sagaId);

			await _orchestrator.CancelSagaAsync(sagaId, "Automatic compensation due to failure", cancellationToken).ConfigureAwait(false);

			// Mark compensation as processed after success
			if (_idempotencyProvider != null)
			{
				await _idempotencyProvider.MarkProcessedAsync(sagaId, compensationKey, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogSagaCompensationFailed(_logger, sagaId, ex);
		}
		finally
		{
			// Remove in-flight claim so that a *new* compensation request for the same saga
			// can proceed if needed (e.g., after a retry). The external idempotency provider
			// is the durable gate; _compensationInFlight is only a process-level TOCTOU guard.
			_compensationInFlight.TryRemove(compensationKey, out _);
		}
	}
}

/// <summary>
/// Attribute to mark a message as part of a saga.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SagaMessageAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the saga type name.
	/// </summary>
	/// <value>The saga type name.</value>
	public string? SagaType { get; set; }
}
