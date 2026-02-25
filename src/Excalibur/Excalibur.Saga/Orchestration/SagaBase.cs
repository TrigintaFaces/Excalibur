// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Abstract base class for implementing saga orchestration patterns in distributed systems. A saga represents a long-running business
/// process that coordinates multiple services through event-driven messaging. This class provides the foundation for managing saga state,
/// handling events, and dispatching commands.
/// </summary>
/// <typeparam name="TSagaState"> The type of saga state that extends SagaState. </typeparam>
/// <param name="initialState"> The initial state of the saga. </param>
/// <param name="dispatcher"> The message dispatcher for sending commands and events. </param>
/// <param name="logger"> Logger instance for saga execution tracking. </param>
public abstract partial class SagaBase<TSagaState>(TSagaState initialState, IDispatcher dispatcher, ILogger logger)
	: Dispatch.Abstractions.Messaging.ISaga<TSagaState>
	where TSagaState : Dispatch.Abstractions.Messaging.SagaState
{
	/// <summary>
	/// Gets the unique identifier of the saga instance.
	/// </summary>
	/// <value>The current <see cref="Id"/> value.</value>
	public Guid Id => State.SagaId;

	/// <summary>
	/// Gets a value indicating whether the saga has completed its orchestration process.
	/// </summary>
	/// <value>The current <see cref="IsCompleted"/> value.</value>
	public bool IsCompleted => State.Completed;

	/// <summary>
	/// Gets the current state of the saga containing all process-specific data and status information.
	/// </summary>
	/// <value>The current <see cref="State"/> value.</value>
	public TSagaState State { get; } = initialState;

	/// <summary>
	/// Gets or sets the timeout store used for scheduling saga timeouts.
	/// Set by the saga coordinator/middleware after construction (property injection).
	/// </summary>
	/// <value>The timeout store, or null if timeouts are not configured.</value>
	protected internal ISagaTimeoutStore? TimeoutStore { get; set; }

	/// <summary>
	/// Gets the message dispatcher used to send commands and publish events as part of the saga orchestration.
	/// </summary>
	/// <value>The current <see cref="Dispatcher"/> value.</value>
	protected IDispatcher Dispatcher { get; } = dispatcher;

	/// <summary>
	/// Gets the logger instance for recording saga execution information, warnings, and errors.
	/// </summary>
	/// <value>The current <see cref="Logger"/> value.</value>
	protected ILogger Logger { get; } = logger;

	/// <summary>
	/// Determines whether the saga can handle the specified event message. Implementations should examine the event type and saga state to
	/// determine message relevance.
	/// </summary>
	/// <param name="eventMessage"> The event message to evaluate. </param>
	/// <returns> true if the saga can handle the event; otherwise, false. </returns>
	public abstract bool HandlesEvent(object eventMessage);

	/// <summary>
	/// Handles the specified event message asynchronously, updating saga state and potentially dispatching new commands. This method
	/// contains the core business logic for the saga's orchestration process.
	/// </summary>
	/// <param name="eventMessage"> The event message to process. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	public abstract Task HandleAsync(object eventMessage, CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a timeout message to be delivered to this saga after the specified delay.
	/// The timeout message will be created using the parameterless constructor of <typeparamref name="TTimeout"/>.
	/// </summary>
	/// <typeparam name="TTimeout">The type of timeout message to schedule. Must have a parameterless constructor.</typeparam>
	/// <param name="delay">The time to wait before delivering the timeout.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A unique timeout identifier that can be used to cancel the timeout.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no timeout store has been configured.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation")]
	protected Task<string> RequestTimeoutAsync<TTimeout>(TimeSpan delay, CancellationToken cancellationToken)
		where TTimeout : class, new()
	{
		return RequestTimeoutAsync<TTimeout>(delay, null, cancellationToken);
	}

	/// <summary>
	/// Schedules a timeout message with custom data to be delivered to this saga after the specified delay.
	/// </summary>
	/// <typeparam name="TTimeout">The type of timeout message to schedule.</typeparam>
	/// <param name="delay">The time to wait before delivering the timeout.</param>
	/// <param name="timeoutData">The timeout data to include in the message, or null for parameterless.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A unique timeout identifier that can be used to cancel the timeout.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no timeout store has been configured.</exception>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation")]
	protected async Task<string> RequestTimeoutAsync<TTimeout>(
		TimeSpan delay,
		TTimeout? timeoutData,
		CancellationToken cancellationToken)
		where TTimeout : class
	{
		if (TimeoutStore is null)
		{
			throw new InvalidOperationException(
				Resources.SagaBase_TimeoutStoreNotConfigured);
		}

		var timeoutId = Guid.NewGuid().ToString();
		var now = DateTimeOffset.UtcNow;

		var timeout = new SagaTimeout(
			TimeoutId: timeoutId,
			SagaId: Id.ToString(),
			SagaType: GetType().AssemblyQualifiedName,
			TimeoutType: typeof(TTimeout).AssemblyQualifiedName,
			TimeoutData: timeoutData is not null ? SerializeTimeoutData(timeoutData) : null,
			DueAt: now.Add(delay),
			ScheduledAt: now);

		await TimeoutStore.ScheduleTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);

		LogTimeoutScheduled(Id, timeoutId, delay);

		return timeoutId;
	}

	/// <summary>
	/// Cancels a previously scheduled timeout.
	/// </summary>
	/// <param name="timeoutId">The unique timeout identifier returned from <see cref="RequestTimeoutAsync{TTimeout}(TimeSpan, CancellationToken)"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no timeout store has been configured.</exception>
	/// <remarks>
	/// Cancellation is idempotent. Cancelling a non-existent or already-delivered timeout completes without error.
	/// </remarks>
	protected async Task CancelTimeoutAsync(string timeoutId, CancellationToken cancellationToken)
	{
		if (TimeoutStore is null)
		{
			throw new InvalidOperationException(
				Resources.SagaBase_TimeoutStoreNotConfigured);
		}

		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		await TimeoutStore.CancelTimeoutAsync(Id.ToString(), timeoutId, cancellationToken).ConfigureAwait(false);

		LogTimeoutCancelled(Id, timeoutId);
	}

	/// <summary>
	/// Marks the saga as completed and logs the completion status. Call this method when the saga has successfully completed its
	/// orchestration process.
	/// </summary>
	/// <remarks>
	/// This method does not cancel pending timeouts. For async-friendly completion with timeout cleanup,
	/// use <see cref="MarkCompletedAsync(CancellationToken)"/>.
	/// </remarks>
	protected void MarkCompleted()
	{
		State.Completed = true;
		LogSagaMarkedAsCompleted(State.SagaId);
	}

	/// <summary>
	/// Marks the saga as completed, cancels all pending timeouts, and logs the completion status.
	/// This is the recommended method for completing sagas that use timeouts.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	protected async Task MarkCompletedAsync(CancellationToken cancellationToken)
	{
		// Cancel all pending timeouts for this saga
		if (TimeoutStore is not null)
		{
			await TimeoutStore.CancelAllTimeoutsAsync(Id.ToString(), cancellationToken).ConfigureAwait(false);

			LogAllTimeoutsCancelled(Id);
		}

		State.Completed = true;

		LogSagaMarkedAsCompleted(State.SagaId);
	}

	/// <summary>
	/// Serializes timeout data for storage. Override to use custom serialization.
	/// </summary>
	/// <typeparam name="T">The type of data to serialize.</typeparam>
	/// <param name="data">The data to serialize.</param>
	/// <returns>The serialized data as a byte array.</returns>
	/// <remarks>
	/// The default implementation uses System.Text.Json. For high-performance scenarios,
	/// consider overriding with MemoryPack or other binary serializers.
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode("JSON serialization may require runtime code generation")]
	protected virtual byte[] SerializeTimeoutData<T>(T data)
	{
		return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data);
	}

	/// <summary>
	/// Sends a command message with saga correlation metadata automatically attached.
	/// </summary>
	/// <typeparam name="TCommand">The type of command to send.</typeparam>
	/// <param name="command">The command message to send.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous operation, containing the message result.</returns>
	/// <remarks>
	/// <para>
	/// This convenience method automatically attaches saga correlation metadata to the message context:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>saga.id</c> - The saga instance identifier (Guid as string)</description></item>
	/// <item><description><c>saga.type</c> - The saga type name (short name, not assembly-qualified)</description></item>
	/// </list>
	/// <para>
	/// The method uses the current ambient message context if available, creating a child context
	/// to properly propagate correlation identifiers. If no ambient context exists, a new context
	/// is created.
	/// </para>
	/// </remarks>
	protected async Task<IMessageResult> SendCommandAsync<TCommand>(
		TCommand command,
		CancellationToken cancellationToken)
		where TCommand : IDispatchMessage
	{
		var context = CreateSagaCorrelatedContext();
		return await Dispatcher.DispatchAsync(command, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Publishes an event message with saga correlation metadata automatically attached.
	/// </summary>
	/// <typeparam name="TEvent">The type of event to publish.</typeparam>
	/// <param name="event">The event message to publish.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous operation, containing the message result.</returns>
	/// <remarks>
	/// <para>
	/// This convenience method automatically attaches saga correlation metadata to the message context:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>saga.id</c> - The saga instance identifier (Guid as string)</description></item>
	/// <item><description><c>saga.type</c> - The saga type name (short name, not assembly-qualified)</description></item>
	/// </list>
	/// <para>
	/// The method uses the current ambient message context if available, creating a child context
	/// to properly propagate correlation identifiers. If no ambient context exists, a new context
	/// is created.
	/// </para>
	/// </remarks>
	protected async Task<IMessageResult> PublishEventAsync<TEvent>(
		TEvent @event,
		CancellationToken cancellationToken)
		where TEvent : IDispatchMessage
	{
		var context = CreateSagaCorrelatedContext();
		return await Dispatcher.DispatchAsync(@event, context, cancellationToken).ConfigureAwait(false);
	}

	// Source-generated logging methods
	// Note: Uses explicit ILogger parameter to avoid CS9124 conflict with primary constructor
	// capturing logger both in protected property and source-generated field.
	[LoggerMessage(SagaEventId.SagaHandlingCompleted, LogLevel.Information,
		"Saga {SagaId} marked as completed")]
	private static partial void LogSagaMarkedAsCompletedCore(ILogger logger, Guid sagaId);

	[LoggerMessage(SagaEventId.SagaInitializationStarted, LogLevel.Debug,
		"Saga {SagaId} scheduled timeout {TimeoutId} for delivery in {Delay}")]
	private static partial void LogTimeoutScheduledCore(ILogger logger, Guid sagaId, string timeoutId, TimeSpan delay);

	[LoggerMessage(SagaEventId.SagaMiddlewareProcessing, LogLevel.Debug,
		"Saga {SagaId} cancelled timeout {TimeoutId}")]
	private static partial void LogTimeoutCancelledCore(ILogger logger, Guid sagaId, string timeoutId);

	[LoggerMessage(SagaEventId.SagaHandlingStarted, LogLevel.Debug,
		"Saga {SagaId} cancelled all pending timeouts")]
	private static partial void LogAllTimeoutsCancelledCore(ILogger logger, Guid sagaId);

	/// <summary>
	/// Creates a message context with saga correlation metadata attached.
	/// </summary>
	/// <returns>A message context with saga correlation metadata.</returns>
	private IMessageContext CreateSagaCorrelatedContext()
	{
		// Try to use the current ambient context if available
		var currentContext = MessageContextHolder.Current;
		IMessageContext context;

		if (currentContext is not null)
		{
			// Create a child context to preserve correlation chain
			context = currentContext.CreateChildContext();
		}
		else
		{
			// Create a new context if no ambient context exists
			context = new MessageContext();
		}

		// Add saga correlation metadata using well-known keys (AD-218-7)
		context.SetItem("saga.id", Id.ToString());
		context.SetItem("saga.type", GetType().Name);

		return context;
	}

	// Wrapper methods for clean call sites
	private void LogSagaMarkedAsCompleted(Guid sagaId) => LogSagaMarkedAsCompletedCore(Logger, sagaId);

	private void LogTimeoutScheduled(Guid sagaId, string timeoutId, TimeSpan delay) =>
		LogTimeoutScheduledCore(Logger, sagaId, timeoutId, delay);

	private void LogTimeoutCancelled(Guid sagaId, string timeoutId) => LogTimeoutCancelledCore(Logger, sagaId, timeoutId);

	private void LogAllTimeoutsCancelled(Guid sagaId) => LogAllTimeoutsCancelledCore(Logger, sagaId);
}
