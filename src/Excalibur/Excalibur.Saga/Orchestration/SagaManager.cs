// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Manages saga lifecycle operations including event handling, state management, and saga instantiation. This component provides
/// lower-level saga management capabilities for direct saga manipulation and serves as a foundation for higher-level saga coordination infrastructure.
/// </summary>
/// <param name="sagaStore"> Persistent store for saga state management and retrieval operations. </param>
/// <param name="serviceProvider"> Service provider for DI-aware saga instantiation via ActivatorUtilities. </param>
/// <param name="loggerFactory"> Factory for creating saga-specific loggers for business logic tracing. </param>
public sealed class SagaManager(ISagaStore sagaStore, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
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
	// MA0038: Cannot make method static - requires access to sagaStore and loggerFactory instance fields from primary constructor
#pragma warning disable MA0038

	// AD-541.4: Use ActivatorUtilities.CreateInstance for DI-aware saga creation (fixes missing IDispatcher)
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
		var sagaState = await sagaStore.LoadAsync<TSagaState>(sagaId, cancellationToken).ConfigureAwait(false) ??
						new TSagaState { SagaId = sagaId };
		var saga = ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, sagaState);

		await saga.HandleAsync(@event, cancellationToken).ConfigureAwait(false);
		await sagaStore.SaveAsync(sagaState, cancellationToken).ConfigureAwait(false);
	}

#pragma warning restore MA0038
}
