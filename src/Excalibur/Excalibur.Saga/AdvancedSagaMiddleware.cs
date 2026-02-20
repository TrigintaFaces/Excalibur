// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Saga.Abstractions;

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
	private readonly ISagaOrchestrator _orchestrator;
	private readonly ISagaStateStore _stateStore;
	private readonly AdvancedSagaOptions _options;
	private readonly ILogger<AdvancedSagaMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdvancedSagaMiddleware"/> class.
	/// </summary>
	/// <param name="orchestrator">The saga orchestrator.</param>
	/// <param name="stateStore">The saga state store.</param>
	/// <param name="options">The saga options.</param>
	/// <param name="logger">The logger.</param>
	public AdvancedSagaMiddleware(
		ISagaOrchestrator orchestrator,
		ISagaStateStore stateStore,
		IOptions<AdvancedSagaOptions> options,
		ILogger<AdvancedSagaMiddleware> logger)
	{
		_orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
		return SagaMessageAttributeCache.GetOrAdd(messageType,
			static type => type.GetCustomAttributes(typeof(SagaMessageAttribute), true).Length > 0);
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

		LogSagaCompensationStarting(_logger, sagaId);

		try
		{
			await _orchestrator.CancelSagaAsync(sagaId, "Automatic compensation due to failure", cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogSagaCompensationFailed(_logger, sagaId, ex);
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
