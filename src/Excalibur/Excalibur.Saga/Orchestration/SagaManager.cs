// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Manages saga lifecycle operations including event handling, state management, and saga instantiation. This component provides
/// lower-level saga management capabilities for direct saga manipulation and serves as a foundation for higher-level saga coordination infrastructure.
/// </summary>
/// <param name="sagaStore"> Persistent store for saga state management and retrieval operations. </param>
/// <param name="serviceProvider"> Service provider for DI-aware saga instantiation via ActivatorUtilities. </param>
/// <param name="loggerFactory"> Factory for creating saga-specific loggers for business logic tracing. Reserved for future use. </param>
internal sealed partial class SagaManager(ISagaStore sagaStore, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
	private readonly ILogger<SagaManager> _logger = loggerFactory.CreateLogger<SagaManager>();
	// The non-keyed ISagaStore is a forwarding alias to the keyed "default" store, and it answers null
	// when nothing backs it — the DI contract for a service that is not registered. Injection is where
	// that absence has to become loud, because from here on the store is dereferenced. Hosts that run
	// hosted services are already stopped at startup by the saga prerequisite validator; this covers the
	// ones that resolve the saga manager without ever starting a host.
	private readonly ISagaStore _sagaStore = sagaStore ?? throw new InvalidOperationException(
		"Sagas are configured but no ISagaStore is registered. Register a persistent saga store (for "
		+ "example a provider's UseSqlServer(...) extension) or opt into the in-memory store via "
		+ "AddInMemorySagaStore().");


	/// <summary>
	/// Handles an event for a specific saga instance by loading state, processing the event, and persisting changes. This method manages
	/// the complete saga event processing lifecycle including state management and saga instantiation. Creates new saga state if none
	/// exists, supporting both saga initiation and continuation scenarios.
	/// </summary>
	/// <typeparam name="TSaga"> Saga implementation type that will process the event. </typeparam>
	/// <typeparam name="TSagaState"> State type for saga persistence and workflow tracking. </typeparam>
	/// <param name="sagaId"> Unique identifier for the saga instance to process the event. </param>
	/// <param name="event"> Event object containing business data for saga processing. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and shutdown support. </param>
	/// <returns> Task representing the asynchronous saga event handling operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when event parameter is null. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when saga cannot be instantiated or state cannot be managed. </exception>
	/// <exception cref="ConcurrencyException"> Thrown when the saga state was modified by another handler between load and save. </exception>
	// MA0038: Cannot make method static - requires access to _sagaStore and serviceProvider instance fields from primary constructor
#pragma warning disable MA0038

	// Use ActivatorUtilities.CreateInstance for DI-aware saga creation (fixes missing IDispatcher)
	[RequiresUnreferencedCode(
		"Uses ActivatorUtilities.CreateInstance to instantiate saga types dynamically. The saga types should be preserved if using AOT.")]
	[RequiresDynamicCode(
		"Uses ActivatorUtilities.CreateInstance which requires runtime code generation for generic type instantiation.")]
	public async Task HandleEventAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSaga,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] TSagaState>(
		Guid sagaId, object @event, CancellationToken cancellationToken)
		where TSaga : SagaBase<TSagaState>
		where TSagaState : SagaState, new()
	{
		// Honor the documented ArgumentNullException contract on EVERY path — including the already-completed
		// skip below, which dereferences @event.GetType and would otherwise NRE on a null event.
		ArgumentNullException.ThrowIfNull(@event);

		var sagaState = await _sagaStore.LoadAsync<TSagaState>(sagaId, cancellationToken).ConfigureAwait(false) ??
						new TSagaState { SagaId = sagaId };

		// A saga that was ALREADY completed before this event does not process further events
		// (SagaState.Completed contract,). Skip at load time without re-persisting -> no
		// HandleAsync, no SaveAsync, no spurious version bump/overwrite on a finished workflow. The event
		// that itself completes the saga still proceeds and persists its completion below. Parity with
		// SagaCoordinator (SagaCoordinator.cs:198).
		if (sagaState.Completed)
		{
			LogSagaAlreadyCompleted(sagaId, @event.GetType().Name);
			return;
		}

		var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, sagaState);

		await saga.HandleAsync(@event, cancellationToken).ConfigureAwait(false);

		// Store-owns-increment (optimistic concurrency,): SagaState.Version is the loaded token; the
		// store atomically compares it and persists the bump (throws ConcurrencyException on mismatch, writes the
		// new version back). No caller version arithmetic -- do NOT re-load to check here, that creates a TOCTOU race.
		await _sagaStore.SaveAsync(sagaState, cancellationToken).ConfigureAwait(false);
	}

#pragma warning restore MA0038

	[LoggerMessage(SagaEventId.SagaAlreadyCompletedEventSkipped, LogLevel.Information,
		"Saga {SagaId} already completed; skipping event {EventType}.")]
	private partial void LogSagaAlreadyCompleted(Guid sagaId, string eventType);
}
