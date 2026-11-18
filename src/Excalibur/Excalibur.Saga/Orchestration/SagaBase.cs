// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
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
	: Dispatch.Messaging.ISaga<TSagaState>
	where TSagaState : Dispatch.Messaging.SagaState
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
	/// Gets the message dispatcher used to flush the buffered commands and events after the saga state is
	/// persisted. Private by design: a saga subclass cannot reach the dispatcher, so it can ONLY emit
	/// via <see cref="SendCommandAsync{TCommand}"/>/<see cref="PublishEventAsync{TEvent}"/> (which buffer) and
	/// dispatch happens solely on the coordinator-driven flush -- making "dispatch-before-save" structurally
	/// inexpressible from saga code.
	/// </summary>
	/// <value>The current <see cref="Dispatcher"/> value.</value>
	private IDispatcher Dispatcher { get; } = dispatcher;

	/// <summary>
	/// Gets the logger instance for recording saga execution information, warnings, and errors.
	/// </summary>
	/// <value>The current <see cref="Logger"/> value.</value>
	protected ILogger Logger { get; } = logger;

	/// <summary>
	/// The message context this saga is currently handling under, set by the coordinator before it
	/// invokes the saga. Passed explicitly rather than read from an ambient flow-local: the saga needs
	/// a parent to correlate emitted commands and events against, the coordinator already holds one,
	/// and handing it over makes the dependency visible instead of implicit.
	/// </summary>
	internal IMessageContext? HandlingContext { get; set; }

	// Save-then-dispatch buffer: commands/events emitted during HandleAsync are queued here with
	// their saga-correlated context and dispatched by the coordinator ONLY after SaveAsync succeeds, in emit
	// (FIFO) order. The saga instance is created fresh per event, so this list is naturally scoped to a single
	// event and single-threaded -- no synchronization needed.
	private readonly List<(IDispatchMessage Message, IMessageContext Context)> _pendingDispatches = [];

	// Save-then-apply buffer for TIMER intents -- the exact counterpart of _pendingDispatches above, and
	// scoped the same way (a saga instance is created fresh per event, so this is single-threaded).
	//
	// A timer effect is DURABLE, so applying one while the handler runs publishes it before the state that
	// references it exists. Both signs of that are reachable with no crash, on every optimistic-concurrency
	// conflict -- which is the designed operating mode of a multi-instance deployment, not an exotic fault:
	// a cancel applied before a failed save leaves the reverted state waiting forever for a row that is
	// already gone, and a schedule applied before a failed save leaves a row no durable state references,
	// so nothing can ever cancel it and its handler runs anyway. Buffering makes both inexpressible from
	// saga code by the same mechanism that already protects emitted messages.
	private readonly List<SagaTimerIntent> _pendingTimerIntents = [];

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
	public abstract Task<SagaEventOutcome> HandleAsync(object eventMessage, CancellationToken cancellationToken);

	/// <summary>
	/// Schedules a timeout message to be delivered to this saga after the specified delay.
	/// The timeout message will be created using the parameterless constructor of <typeparamref name="TTimeout"/>.
	/// </summary>
	/// <typeparam name="TTimeout">The type of timeout message to schedule. Must have a parameterless constructor.</typeparam>
	/// <param name="delay">The time to wait before delivering the timeout.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A unique timeout identifier that can be used to cancel the timeout.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no timeout store has been configured.</exception>
	protected Task<string> RequestTimeoutAsync<TTimeout>(TimeSpan delay, CancellationToken cancellationToken)
		where TTimeout : class, new()
	{
		// No payload, so no serializer runs on this path: schedule directly rather than through the
		// data-carrying overload, whose trim/AOT requirement comes solely from serializing the payload.
		return ScheduleTimeoutAsync<TTimeout>(delay, null, cancellationToken);
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
	protected Task<string> RequestTimeoutAsync<TTimeout>(
		TimeSpan delay,
		TTimeout? timeoutData,
		CancellationToken cancellationToken)
		where TTimeout : class
	{
		return ScheduleTimeoutAsync<TTimeout>(
			delay,
			timeoutData is not null ? SerializeTimeoutData(timeoutData) : null,
			cancellationToken);
	}

	/// <summary>
	/// Schedules a timeout whose payload has already been serialized. Carries no trim or AOT
	/// requirement: the type name is captured statically and nothing here reflects over it.
	/// </summary>
	private Task<string> ScheduleTimeoutAsync<TTimeout>(
		TimeSpan delay,
		byte[]? serializedTimeoutData,
		CancellationToken cancellationToken)
		where TTimeout : class
	{
		if (TimeoutStore is null)
		{
			throw new InvalidOperationException(
				Resources.SagaBase_TimeoutStoreNotConfigured);
		}

		// Observed here rather than by the store call this no longer makes, so a cancelled handler still
		// declines to record an intent instead of buffering one it never meant to commit.
		cancellationToken.ThrowIfCancellationRequested();

		var timeoutId = Guid.NewGuid().ToString();
		var now = DateTimeOffset.UtcNow;

		var timeout = new SagaTimeout(
			TimeoutId: timeoutId,
			SagaId: Id.ToString(),
			SagaType: GetType().AssemblyQualifiedName!,
			TimeoutType: typeof(TTimeout).AssemblyQualifiedName!,
			TimeoutData: serializedTimeoutData,
			DueAt: now.Add(delay),
			ScheduledAt: now);

		// Buffered, not applied -- see _pendingTimerIntents. The identifier is returned NOW because the
		// handler stores it in saga state, and that state is committed by the same save that gates the
		// apply: either both exist afterwards or neither does.
		_pendingTimerIntents.Add(new SagaTimerIntent(SagaTimerAction.Schedule, timeoutId, timeout));

		return Task.FromResult(timeoutId);
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
	protected Task CancelTimeoutAsync(string timeoutId, CancellationToken cancellationToken)
	{
		if (TimeoutStore is null)
		{
			throw new InvalidOperationException(
				Resources.SagaBase_TimeoutStoreNotConfigured);
		}

		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		cancellationToken.ThrowIfCancellationRequested();

		// Buffered, not applied -- see _pendingTimerIntents. This is the parked-forever sign: the row the
		// reverted state still expects would otherwise already be gone.
		_pendingTimerIntents.Add(new SagaTimerIntent(SagaTimerAction.Cancel, timeoutId, null));

		return Task.CompletedTask;
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
	protected Task MarkCompletedAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Buffered, not applied -- see _pendingTimerIntents. Completion is the same hazard from a third
		// side: this retires EVERY pending row, so running it before the save that records the completion
		// can strand a saga whose state still expects them all.
		if (TimeoutStore is not null)
		{
			_pendingTimerIntents.Add(new SagaTimerIntent(SagaTimerAction.CancelAll, string.Empty, null));
		}

		State.Completed = true;

		LogSagaMarkedAsCompleted(State.SagaId);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Records the business key this instance is correlated on, so it can be found by that key later.
	/// </summary>
	/// <param name="correlationKey">The business key, the same one passed to <see cref="SagaCorrelation.ToSagaId(string, string)"/>.</param>
	/// <remarks>
	/// <para>
	/// This does not affect routing. An event reaches this instance because its identifier was DERIVED
	/// from the key, so the ordinary load-by-identifier path already found it with no query. What this adds
	/// is the reverse direction, which derivation cannot give: the identifier is a one-way function of the
	/// key, so without recording the key nothing can say which order or subscription a given saga belongs
	/// to. An operator looking at a stuck saga needs that, and so does anyone searching for the saga that
	/// owns a known business key.
	/// </para>
	/// <para>
	/// Call it once, where the instance is first created. The SQL Server provider already indexes this
	/// value through a persisted computed column over the state JSON.
	/// </para>
	/// </remarks>
	protected void CorrelateWith(string correlationKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(correlationKey);

		State.CorrelationId = correlationKey;
	}

	/// <summary>
	/// Declares the compensating action that undoes <paramref name="stepName"/>, by name.
	/// </summary>
	/// <param name="stepName">The step this action undoes.</param>
	/// <param name="compensate">The action that undoes the step's committed work.</param>
	/// <remarks>
	/// <para>
	/// Call this from the saga's constructor, for every step that can commit something a later failure
	/// must undo. Registration is per instance and re-done on every load, which is exactly why the
	/// delegate can be a plain closure: it is created fresh by the constructor the store just ran, so
	/// nothing has to be serialized and nothing has to be resolved by reflection. <b>That is what makes
	/// this work under trimming and Native AOT</b>, where looking a method up by name at runtime does not.
	/// </para>
	/// <para>
	/// <b>Registering an action is not the same as owing one.</b> This says what undoing a step MEANS;
	/// <see cref="RecordPendingCompensation"/> says the step actually ran and the undo is now owed, and
	/// that half lives in the persisted state. Keeping them apart is what lets a saga reloaded in a fresh
	/// process still know what it owes without anyone having to serialize a delegate.
	/// </para>
	/// </remarks>
	protected void RegisterCompensation(string stepName, Func<CancellationToken, Task> compensate)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
		ArgumentNullException.ThrowIfNull(compensate);

		_compensations[stepName] = compensate;
	}

	/// <summary>
	/// Records that <paramref name="stepName"/> has committed work which a later failure must undo.
	/// </summary>
	/// <param name="stepName">The step whose compensating action is now owed.</param>
	/// <remarks>
	/// Call this AFTER the step's work has been performed. Recording first and failing second would owe an
	/// undo for work that never happened, and undoing work that was never done is its own defect. It writes
	/// to the current in-memory state. The obligation becomes durable only when that state is saved;
	/// this method does not itself persist it or atomically commit an external side effect with it. A
	/// crash between the external effect and the state save remains a recovery responsibility. Use
	/// transactional coordination where available and idempotent step and compensation operations.
	/// </remarks>
	protected void RecordPendingCompensation(string stepName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

		State.RecordPendingCompensation(stepName);
	}

	/// <summary>
	/// Runs every owed compensating action, most recent step first, and clears the obligation.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a step owes a compensation this instance did not register, which means committed work
	/// has no reachable undo.
	/// </exception>
	/// <exception cref="AggregateException">
	/// Thrown when one or more compensating actions failed. Every action is attempted first.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Reverse order, because a later step's work routinely depends on an earlier step's: undoing forwards
	/// either fails on a dependency still in place, or succeeds and orphans the earlier work.
	/// </para>
	/// <para>
	/// <b>An owed step with no registered action THROWS; it is never skipped.</b> Skipping is the worst
	/// available outcome: the obligation is cleared, the committed work stays, and nothing downstream can
	/// tell that apart from a saga that compensated cleanly. The reachable cause is a saga edited so a step
	/// still records an obligation its constructor no longer registers, which is a code defect and is
	/// surfaced as one.
	/// </para>
	/// <para>
	/// <b>One failing action does not strand the rest.</b> Every owed action is attempted, failures are
	/// collected, and the aggregate is thrown at the end, so a single unreachable downstream service cannot
	/// leave earlier steps un-undone as collateral.
	/// </para>
	/// <para>
	/// Commands and events emitted through <see cref="SendCommandAsync{TCommand}"/> and
	/// <see cref="PublishEventAsync{TEvent}"/> use the saga's buffered save-then-dispatch path. Direct
	/// external calls made by an action do not acquire that persistence boundary. Pending obligations are
	/// taken from the in-memory state before actions run; this method does not provide automatic durable
	/// retry of failed compensations. The host must preserve or reconstruct retry obligations and use
	/// idempotent actions when recovering from failures or crashes.
	/// </para>
	/// </remarks>
	protected async Task CompensateAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var owed = State.TakePendingCompensations();
		if (owed.Count == 0)
		{
			return;
		}

		List<Exception>? failures = null;

		foreach (var stepName in owed)
		{
			if (!_compensations.TryGetValue(stepName, out var compensate))
			{
				throw new InvalidOperationException(
					$"Saga '{State.SagaId}' owes a compensating action for step '{stepName}', but no action "
					+ "is registered for that name on this instance, so the work that step committed has no "
					+ "reachable undo. Register it with RegisterCompensation in the saga's constructor, "
					+ "using the same name passed to RecordPendingCompensation.");
			}

			try
			{
				await compensate(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // Every owed action is attempted; the aggregate is thrown below.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				(failures ??= []).Add(ex);
			}
		}

		if (failures is not null)
		{
			throw new AggregateException(
				$"Saga '{State.SagaId}' failed to run {failures.Count} of {owed.Count} compensating "
				+ "actions. Every action was attempted, and the state no longer records them as owed, so a "
				+ "retry must be driven by the host rather than by reloading the saga.",
				failures);
		}
	}

	private readonly Dictionary<string, Func<CancellationToken, Task>> _compensations =
		new(StringComparer.Ordinal);

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
	/// <returns>A task that completes once the message has been buffered for save-then-dispatch; the message is dispatched by the coordinator after the saga state is persisted.</returns>
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
	protected Task SendCommandAsync<TCommand>(
		TCommand command,
		CancellationToken cancellationToken)
		where TCommand : IDispatchMessage
	{
		// Save-then-dispatch: buffer the command with its saga-correlated context captured NOW
		// (correlation reflects the handling moment). The SagaCoordinator flushes the buffer only AFTER the
		// state save is acknowledged. A postcommit acknowledgement/flush failure is not rollback and can
		// strand this ordinary volatile buffer; the selected atomic profile instead prepares durable writes.
		return BufferEmissionAsync(command, cancellationToken);
	}

	/// <summary>
	/// Publishes an event message with saga correlation metadata automatically attached.
	/// </summary>
	/// <typeparam name="TEvent">The type of event to publish.</typeparam>
	/// <param name="event">The event message to publish.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that completes once the message has been buffered for save-then-dispatch; the message is dispatched by the coordinator after the saga state is persisted.</returns>
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
	protected Task PublishEventAsync<TEvent>(
		TEvent @event,
		CancellationToken cancellationToken)
		where TEvent : IDispatchMessage
	{
		// Save-then-dispatch: see SendCommandAsync. Events share the same per-event FIFO buffer and
		// are dispatched by the coordinator only after the saga state is persisted.
		return BufferEmissionAsync(@event, cancellationToken);
	}

	/// <summary>
	/// Captures the original buffered timer intents in request order without applying or clearing them.
	/// Recovery uses this snapshot to persist the original intent identities before receiver application.
	/// </summary>
	/// <returns>An owned array snapshot of the ordered buffered timer intents.</returns>
    internal SagaTimerIntent[] CaptureOrdinaryTimerIntents() => _pendingTimerIntents.ToArray();

    internal void ClearOrdinaryTimerIntents() => _pendingTimerIntents.Clear();

	internal async Task FlushPendingTimerIntentsAsync(CancellationToken cancellationToken)
	{
		if (TimeoutStore is null)
		{
			// Nothing could have been buffered: both entry points refuse without a store, and completion
			// buffers nothing. Clearing is for the case where a store was removed between the two.
			_pendingTimerIntents.Clear();

			return;
		}

		foreach (var intent in _pendingTimerIntents)
		{
			switch (intent.Action)
			{
				case SagaTimerAction.Schedule:
					var timeout = intent.Timeout!;
					await TimeoutStore.ScheduleTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
					LogTimeoutScheduled(Id, intent.TimeoutId, timeout.DueAt - timeout.ScheduledAt);
					break;

				case SagaTimerAction.Cancel:
					await TimeoutStore.CancelTimeoutAsync(Id.ToString(), intent.TimeoutId, cancellationToken)
						.ConfigureAwait(false);
					LogTimeoutCancelled(Id, intent.TimeoutId);
					break;

				case SagaTimerAction.CancelAll:
					await TimeoutStore.CancelAllTimeoutsAsync(Id.ToString(), cancellationToken).ConfigureAwait(false);
					LogAllTimeoutsCancelled(Id);
					break;

				default:
					// Unreachable today, and deliberately loud rather than a silent skip: a fourth action
					// added without an arm here would otherwise be buffered and never applied.
					throw new InvalidOperationException(
						string.Create(
							System.Globalization.CultureInfo.InvariantCulture,
							$"Unhandled saga timer action {intent.Action}."));
			}
		}

		_pendingTimerIntents.Clear();
	}

	/// <summary>
	/// Dispatches all commands and events buffered during event handling, in emit (FIFO) order. Invoked by the
	/// saga coordinator ONLY after the saga state has been durably persisted (save-then-dispatch), so a
	/// known precommit failure dispatches nothing; postcommit uncertainty is not a replay guarantee on the next
	/// delivery. Internal by design: a saga subclass cannot trigger dispatch, which makes "dispatch-before-save"
	/// structurally inexpressible.
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous flush operation.</returns>
	internal async Task FlushPendingDispatchesAsync(CancellationToken cancellationToken)
	{
		foreach (var (message, context) in _pendingDispatches)
		{
			var result = await Dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			if (result.Succeeded)
			{
				continue;
			}

			// The dispatcher CAN report failure and this result used to be discarded, which made a lost
			// saga message indistinguishable from a delivered one at every log level. It is a genuine
			// loss, not a retry: the state is already committed and the triggering event is already
			// marked processed, so a redelivery returns at the duplicate check and never re-buffers this
			// message. Reported rather than raised -- raising here would surface an error for work that
			// is durably committed, and would not recover the message either, because the redelivery it
			// provokes is the one the duplicate check refuses. Warning rather than Error because the
			// consumer obligation stated in the guarantee doc is to trigger must-not-be-lost work from a
			// durable record instead of from this dispatch.
			LogEmittedMessageNotDispatched(Id, message.GetType().Name, result.ErrorMessage);
		}

		_pendingDispatches.Clear();
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

	[LoggerMessage(SagaEventId.SagaEmittedMessageNotDispatched, LogLevel.Warning,
		"Saga {SagaId} emitted {MessageType} and the dispatcher reported it unsuccessful after the state "
		+ "was committed, so the message is lost and will not be re-emitted: {ErrorMessage}")]
	private static partial void LogEmittedMessageNotDispatchedCore(
		ILogger logger, Guid sagaId, string messageType, string? errorMessage);

	/// <summary>
	/// Creates a message context with saga correlation metadata attached.
	/// </summary>
	/// <returns>A message context with saga correlation metadata.</returns>
	private IMessageContext CreateSagaCorrelatedContext()
	{
		// The context the coordinator is handling under, handed to us explicitly.
		var currentContext = HandlingContext;
		IMessageContext context;

		if (currentContext is not null)
		{
			// Create a child context to preserve correlation chain
			context = currentContext.CreateChildContext();
			// Timeout authority belongs to the inbound delivery, never to its new emissions.
			SagaTimeoutDeliveryMetadata.RemoveFromOwnedChild(context);
		}
		else
		{
			// Create a new context if no ambient context exists
			context = new MessageContext();
		}

		// Add saga correlation metadata using well-known keys
		context.SetItem("saga.id", Id.ToString());
		context.SetItem("saga.type", GetType().Name);

		return context;
	}

	// Wrapper methods for clean call sites
	private void LogSagaMarkedAsCompleted(Guid sagaId) => LogSagaMarkedAsCompletedCore(Logger, sagaId);

	private void LogEmittedMessageNotDispatched(Guid sagaId, string messageType, string? errorMessage) =>
		LogEmittedMessageNotDispatchedCore(Logger, sagaId, messageType, errorMessage);

	private void LogTimeoutScheduled(Guid sagaId, string timeoutId, TimeSpan delay) =>
		LogTimeoutScheduledCore(Logger, sagaId, timeoutId, delay);

	private void LogTimeoutCancelled(Guid sagaId, string timeoutId) => LogTimeoutCancelledCore(Logger, sagaId, timeoutId);

	private void LogAllTimeoutsCancelled(Guid sagaId) => LogAllTimeoutsCancelledCore(Logger, sagaId);
}

/// <summary>
/// One buffered timer effect. The action is explicit rather than inferred from which fields are set: a
/// cancellation and a schedule carrying no payload are otherwise the same shape.
/// </summary>
/// <param name="Action">Which timer effect to apply.</param>
/// <param name="TimeoutId">The timeout identifier, or an empty string for a cancel-all.</param>
/// <param name="Timeout">The timeout to schedule, or null for either cancellation.</param>
internal readonly record struct SagaTimerIntent(SagaTimerAction Action, string TimeoutId, SagaTimeout? Timeout);

/// <summary>
/// The kinds of timer effect a saga can request.
/// </summary>
internal enum SagaTimerAction
{
	/// <summary>Schedule the carried timeout.</summary>
	Schedule,

	/// <summary>Cancel one timeout by identifier.</summary>
	Cancel,

	/// <summary>Cancel every pending timeout for the saga.</summary>
	CancelAll,
}
