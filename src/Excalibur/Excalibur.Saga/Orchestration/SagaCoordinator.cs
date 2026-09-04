// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Diagnostics;
using Excalibur.Saga.Handlers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Coordinates saga execution by processing saga events and managing saga state transitions. This component serves as the main orchestrator
/// for long-running business processes implemented as sagas, handling event routing, saga instantiation, state management, and completion tracking.
/// </summary>
/// <param name="serviceProvider"> Service provider for dependency injection and saga instantiation. </param>
/// <param name="sagaStore"> Persistent store for saga state management and retrieval. </param>
/// <param name="options"> Saga runtime options (concurrency, timeout, and retry policy) applied to event processing. </param>
/// <param name="logger"> Logger for saga coordination activities, errors, and performance metrics. </param>
public sealed partial class SagaCoordinator(IServiceProvider serviceProvider, ISagaStore sagaStore, IOptions<SagaOptions> options, ILogger<SagaCoordinator> logger)
	: ISagaCoordinator, IDisposable
{
	private const int MaxCacheEntries = 1024;
	// The non-keyed ISagaStore is a forwarding alias to the keyed "default" store, and it answers null
	// when nothing backs it — the DI contract for a service that is not registered. Injection is where
	// that absence has to become loud, because from here on the store is dereferenced. Hosts that run
	// hosted services are already stopped at startup by the saga prerequisite validator; this covers the
	// ones that resolve the coordinator without ever starting a host.
	private readonly ISagaStore _sagaStore = sagaStore ?? throw new InvalidOperationException(
		"Sagas are configured but no ISagaStore is registered. Register a persistent saga store (for "
		+ "example a provider's UseSqlServer(...) extension) or opt into the in-memory store via "
		+ "AddInMemorySagaStore().");


	// Runtime policy from SagaOptions: concurrency gate + per-event timeout + bounded retry,
	// applied uniformly in ProcessEventAsync. Singleton coordinator → the semaphore is a process-wide gate.
	private readonly SagaOptions _options = options.Value;
	private readonly SemaphoreSlim _concurrencyGate = new(options.Value.MaxConcurrency, options.Value.MaxConcurrency);

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Bucket D: a static field initializer cannot carry RequiresUnreferencedCode, and this GetMethod call reflects only over this type's own member, which the JIT path at MakeGenericMethod requires. The AOT path does not reach it - it uses ISagaDispatchRegistry instead.")]
	private static readonly MethodInfo HandleEventInternalMethodInfo =
		typeof(SagaCoordinator).GetMethod(
			nameof(HandleEventInternalAsync),
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

	private static readonly ConcurrentDictionary<(Type SagaType, Type StateType), MethodInfo> GenericMethodCache = new();

	private static readonly ConcurrentDictionary<(Type SagaType, Type EventType), MethodInfo?> TimeoutHandlerCache = new();

	/// <summary>
	/// Processes a saga event by routing it to the appropriate saga instance and managing state transitions. This method handles saga
	/// discovery, instantiation, event processing, and state persistence for long-running business processes. Supports both saga initiation
	/// events and continuation events with proper error handling and logging.
	/// </summary>
	/// <param name="messageContext"> Message context containing routing, tracing, and correlation information. </param>
	/// <param name="evt"> Saga event to process, containing business data and saga correlation identifier. </param>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task representing the asynchronous saga event processing operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when messageContext or evt is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga configuration is invalid or saga cannot be instantiated. </exception>
	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection to invoke generic HandleEventAsyncInternal method with runtime types")]
	[RequiresDynamicCode("This method uses MakeGenericMethod with runtime types")]
	public async Task ProcessEventAsync(
		IMessageContext messageContext,
		ISagaEvent evt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageContext);
		ArgumentNullException.ThrowIfNull(evt);

		var sagaType = SagaRegistry.GetSagaTypeForEvent(evt.GetType());
		if (sagaType is null)
		{
			LogNoSagaRegistered(evt.GetType().Name);

			return;
		}

		var sagaInfo = SagaRegistry.GetSagaInfo(sagaType);
		if (sagaInfo is null)
		{
			LogNoSagaMetadata(sagaType.Name);

			return;
		}

		var sagaStateType = sagaInfo.StateType;

		// Apply the SagaOptions runtime policy: bound global concurrency, a per-event timeout,
		// and bounded retry around the actual saga dispatch. Resolution above (registry lookups) is not
		// gated/retried — only the handler execution is.
		await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await RunWithTimeoutAndRetryAsync(DispatchCoreAsync, evt.GetType().Name, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = _concurrencyGate.Release();
		}

		return;

		async Task DispatchCoreAsync(CancellationToken ct)
		{
			// AOT path: use pre-registered typed dispatch delegate
			if (!RuntimeFeature.IsDynamicCodeSupported)
			{
				var registry = serviceProvider.GetService<ISagaDispatchRegistry>();
				var dispatcher = registry?.GetDispatcher(sagaType, sagaStateType)
					?? throw new PlatformNotSupportedException(
						$"Saga dispatch for {sagaType.Name}/{sagaStateType.Name} requires a typed dispatch registration " +
						"in AOT mode. Register via ISagaDispatchRegistry at DI time or use the SagaRegistrationGenerator source generator.");

				await dispatcher(this, messageContext, evt, sagaInfo, ct).ConfigureAwait(false);
				return;
			}

			// JIT path: use cached MakeGenericMethod
			var cacheKey = (sagaType, sagaStateType);

			MethodInfo method;
			if (GenericMethodCache.TryGetValue(cacheKey, out var cached))
			{
				method = cached;
			}
			else
			{
				method = HandleEventInternalMethodInfo.MakeGenericMethod(sagaType, sagaStateType);

				// Cache the method if under the limit; TryAdd is a no-op if another thread added it first
				if (GenericMethodCache.Count < MaxCacheEntries)
				{
					GenericMethodCache.TryAdd(cacheKey, method);
				}
			}

			var result = method.Invoke(this, [messageContext, evt, sagaInfo, ct]);
			if (result is not Task task)
			{
				throw new InvalidOperationException(
					$"Expected Task from {method.DeclaringType?.Name}.{method.Name} but got {result?.GetType().Name ?? "null"}");
			}

			await task.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Executes the saga dispatch under the configured <see cref="SagaOptions.DefaultTimeout"/> and bounded
	/// retry (<see cref="SagaOptions.MaxRetryAttempts"/> / <see cref="SagaOptions.RetryDelay"/>). A per-attempt
	/// timeout cancels a hung handler; caller-driven cancellation is never retried.
	/// </summary>
	private async Task RunWithTimeoutAndRetryAsync(
		Func<CancellationToken, Task> action,
		string eventType,
		CancellationToken cancellationToken)
	{
		var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);
		for (var attempt = 1; ; attempt++)
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (_options.DefaultTimeout > TimeSpan.Zero)
			{
				timeoutCts.CancelAfter(_options.DefaultTimeout);
			}

			try
			{
				await action(timeoutCts.Token).ConfigureAwait(false);
				return;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Caller-driven cancellation (shutdown) — not a retryable saga failure.
				throw;
			}
			catch (Exception ex) when (attempt < maxAttempts)
			{
				logger.LogWarning(
					ex,
					"Saga event {EventType} processing attempt {Attempt}/{MaxAttempts} failed; retrying after {RetryDelay}.",
					eventType,
					attempt,
					maxAttempts,
					_options.RetryDelay);

				if (_options.RetryDelay > TimeSpan.Zero)
				{
					await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}

	/// <inheritdoc />
	public void Dispose() => _concurrencyGate.Dispose();

	/// <summary>
	/// Internal generic method that handles saga event processing with strongly-typed saga and state types. This method manages the
	/// complete saga lifecycle including state loading, event processing, and persistence. Handles both saga initiation (creating new
	/// state) and continuation (loading existing state) scenarios.
	/// </summary>
	/// <typeparam name="TSaga"> Specific saga type that will process the event. </typeparam>
	/// <typeparam name="TSagaState"> State type associated with the saga for persistence. </typeparam>
	/// <param name="messageContext"> Message context providing correlation and routing information. </param>
	/// <param name="evt"> Saga event containing business data and correlation identifiers. </param>
	/// <param name="sagaInfo"> Metadata about the saga including event handling capabilities. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and shutdown support. </param>
	/// <returns> Task representing the asynchronous saga event processing operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga cannot be instantiated or state cannot be managed. </exception>
	[RequiresUnreferencedCode("Uses reflection to instantiate saga types")]
	[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Saga types are preserved through configuration and DI registration")]
	[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with RequiresDynamicCodeAttribute may break when trimming",
		Justification = "Saga types are preserved through configuration")]
	public async Task HandleEventInternalAsync<TSaga, TSagaState>(
		IMessageContext messageContext,
		ISagaEvent evt,
		SagaInfo sagaInfo,
		CancellationToken cancellationToken)
		where TSaga : SagaBase<TSagaState>
		where TSagaState : SagaState, new()
	{
		ArgumentNullException.ThrowIfNull(messageContext);
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(sagaInfo);

		TSagaState? sagaState;

		var isStartEvent = sagaInfo.IsStartEvent(evt.GetType());

		if (isStartEvent)
		{
			// A start event for an ALREADY-EXISTING saga is a replayed/duplicate delivery, not a new
			// instance. Load the existing state first so the persisted idempotent-replay guard
			// (ProcessedEventIds) and the optimistic-concurrency version token both apply; only create a
			// fresh state when no saga exists yet. Without this, a replayed start event news up a
			// version-0 state that then collides on save with the already-persisted saga
			// (ConcurrencyException: expected version 0, actual version 1).
			var sagaId = Guid.Parse(evt.SagaId);
			sagaState = await _sagaStore.LoadAsync<TSagaState>(sagaId, cancellationToken).ConfigureAwait(false)
				?? new TSagaState { SagaId = sagaId };
		}
		else
		{
			sagaState = await _sagaStore.LoadAsync<TSagaState>(Guid.Parse(evt.SagaId), cancellationToken).ConfigureAwait(false);
			if (sagaState is null)
			{
				// Invoke the registered ISagaNotFoundHandler<TSaga> extension point.
				// The default LoggingNotFoundHandler<TSaga> is registered out of the box, so this is normally
				// satisfiable and logs the not-found event. A consumer can register a custom handler to
				// dead-letter / park / compensate the orphaned continuation instead of silently dropping it.
				// Fail-open: if no handler is resolvable, fall back to the bare warning log (behavior preserved).
				var notFoundHandler = serviceProvider.GetService<ISagaNotFoundHandler<TSaga>>();
				if (notFoundHandler is not null)
				{
					await notFoundHandler.HandleAsync(evt, evt.SagaId, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					LogSagaNotFound(evt.SagaId, evt.GetType().Name);
				}

				return;
			}
		}

		// A saga that was ALREADY completed before this event does not process further events
		// (SagaState.Completed contract). Skip without re-persisting, which also avoids a spurious version
		// bump/overwrite on a finished workflow. This is checked at load time, so the event that itself
		// completes the saga still proceeds and persists its completion below.
		if (sagaState.Completed)
		{
			LogSagaAlreadyCompleted(evt.SagaId, evt.GetType().Name);
			return;
		}

		var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, sagaState);

		// Hand the saga the context it is handling under, so the commands and events it emits can be
		// correlated as children of this one. Previously read from a flow-local static inside the saga.
		saga.HandlingContext = messageContext;

		// Idempotent replay guard: derive a unique event ID and check if already processed.
		// The ID is added to the in-memory set BEFORE HandleAsync, but only persisted when SaveAsync succeeds.
		// If SaveAsync fails or crashes, the ID is lost from the set on reload, allowing correct replay.
		var eventId = DeriveEventId(evt);
		if (!sagaState.TryMarkEventProcessed(eventId))
		{
			LogDuplicateEventSkipped(evt.SagaId, eventId);
			return;
		}

		// Check for ISagaTimeout<TEvent> strongly-typed handler first.
		// If the saga implements ISagaTimeout<T> for this event type, use the timeout handler.
		// Otherwise, fall through to the general HandleAsync path.
		if (TryInvokeTimeoutHandler(saga, evt, cancellationToken, out var timeoutTask))
		{
			await timeoutTask.ConfigureAwait(false);
		}
		else
		{
			if (!saga.HandlesEvent(evt))
			{
				LogSagaNotHandled(evt.SagaId, evt.GetType().Name);

				return;
			}

			await saga.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
		}

		// Store-owns-increment (optimistic concurrency,): SagaState.Version is the loaded token; the
		// store compares it and persists the bump (writing the new version back), throwing ConcurrencyException if
		// another handler advanced the saga since we loaded it. No caller version arithmetic.
		await _sagaStore.SaveAsync(sagaState, cancellationToken).ConfigureAwait(false);

		// Save-then-dispatch: the saga buffered the commands/events it emitted during HandleAsync;
		// now that its state is durably persisted, flush them in emit order. A SaveAsync failure above throws
		// before reaching here -> nothing was dispatched, and the messages re-buffer on the next delivery.
		await saga.FlushPendingDispatchesAsync(cancellationToken).ConfigureAwait(false);

		if (saga.IsCompleted)
		{
			LogSagaCompleted(evt.SagaId);
		}
	}

	/// <summary>
	/// Attempts to invoke a strongly-typed timeout handler (<see cref="ISagaTimeout{TMessage}"/>)
	/// on the saga for the given event. Returns true if the saga implements the timeout handler
	/// interface for this event type.
	/// </summary>
	/// <param name="saga">The saga instance.</param>
	/// <param name="evt">The event that may be a timeout message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="task">The task from the timeout handler invocation, or null if not handled.</param>
	/// <returns>True if a timeout handler was found and invoked; false otherwise.</returns>
	[RequiresUnreferencedCode("Uses reflection to resolve ISagaTimeout<T> interface and invoke HandleTimeoutAsync")]
	[RequiresDynamicCode("Uses MakeGenericType with runtime event types")]
	private static bool TryInvokeTimeoutHandler(
		object saga,
		ISagaEvent evt,
		CancellationToken cancellationToken,
		[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Task? task)
	{
		var sagaType = saga.GetType();
		var evtType = evt.GetType();
		var cacheKey = (sagaType, evtType);

		// Check cache for the HandleTimeoutAsync method (null = not a timeout handler)
		if (!TimeoutHandlerCache.TryGetValue(cacheKey, out var method))
		{
			var timeoutInterfaceType = typeof(ISagaTimeout<>).MakeGenericType(evtType);

			if (timeoutInterfaceType.IsAssignableFrom(sagaType))
			{
				method = timeoutInterfaceType.GetMethod(nameof(ISagaTimeout<>.HandleTimeoutAsync));
			}

			// Cache the result (null means "not a timeout handler for this event type")
			if (TimeoutHandlerCache.Count < MaxCacheEntries)
			{
				TimeoutHandlerCache.TryAdd(cacheKey, method);
			}
		}

		if (method is null)
		{
			task = null;
			return false;
		}

		task = (Task)method.Invoke(saga, [evt, cancellationToken])!;
		return true;
	}

	/// <summary>
	/// Derives a unique event identifier for idempotent replay detection.
	/// Uses the event type name, saga ID, and step ID to produce a deterministic key.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <see cref="ISagaEvent.StepId"/> is <see langword="null"/>, the derived ID
	/// uses only the event type name and saga ID: <c>{EventType}:{SagaId}</c>.
	/// This means that if the same saga receives multiple events of the same type
	/// (but for different steps) without a <c>StepId</c>, only the first will be
	/// processed -- subsequent deliveries will be treated as duplicates.
	/// </para>
	/// <para>
	/// To ensure correct deduplication when a saga handles the same event type in
	/// multiple steps, always set <see cref="ISagaEvent.StepId"/> to a unique value
	/// per step (e.g., the step name or ordinal).
	/// </para>
	/// </remarks>
	private static string DeriveEventId(ISagaEvent evt)
	{
		// Combine type + sagaId + stepId for a deterministic unique key per saga event delivery
		return evt.StepId is not null
			? $"{evt.GetType().Name}:{evt.SagaId}:{evt.StepId}"
			: $"{evt.GetType().Name}:{evt.SagaId}";
	}

	// Source-generated logging methods
	[LoggerMessage(SagaEventId.SagaStepFailedStartingCompensation, LogLevel.Warning,
		"Saga {SagaId} not found for event {EventType}.")]
	private partial void LogSagaNotFound(string sagaId, string eventType);

	[LoggerMessage(SagaEventId.SagaStepExecuting, LogLevel.Warning,
		"Saga {SagaId} does not handle event {EventType}.")]
	private partial void LogSagaNotHandled(string sagaId, string eventType);

	[LoggerMessage(SagaEventId.SagaCompensationCompleted, LogLevel.Information,
		"Saga {SagaId} completed and persisted.")]
	private partial void LogSagaCompleted(string sagaId);

	[LoggerMessage(SagaEventId.SagaExecutionStarting, LogLevel.Warning,
		"No saga registered for event type {EventType}")]
	private partial void LogNoSagaRegistered(string eventType);

	[LoggerMessage(SagaEventId.SagaExecutionFailed, LogLevel.Error,
		"No saga metadata configured for saga type {SagaType}")]
	private partial void LogNoSagaMetadata(string sagaType);

	[LoggerMessage(SagaEventId.SagaDuplicateEventSkipped, LogLevel.Information,
		"Saga {SagaId} skipped duplicate event {EventId} (idempotent replay protection)")]
	private partial void LogDuplicateEventSkipped(string sagaId, string eventId);

	[LoggerMessage(SagaEventId.SagaAlreadyCompletedEventSkipped, LogLevel.Information,
		"Saga {SagaId} already completed; skipping event {EventType}.")]
	private partial void LogSagaAlreadyCompleted(string sagaId, string eventType);
}
