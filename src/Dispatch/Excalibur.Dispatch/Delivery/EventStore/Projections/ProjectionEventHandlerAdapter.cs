// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Delivery;

// CA1005: The three type parameters are necessary for type-safe event sourcing projections: TProjection - The projection type, TKey - The
// aggregate/projection key type, TEvent - The specific event type These cannot be reduced without losing compile-time type safety in the
// event handling pipeline.
// R0.8: Avoid excessive parameters on generic types
#pragma warning disable CA1005

/// <summary>
/// Provides a type-safe adapter for handling projection events within the event sourcing infrastructure. This adapter bridges between the
/// generic projection handling system and specific event types, ensuring compile-time type safety while enabling polymorphic event processing.
/// </summary>
/// <typeparam name="TProjection"> The type of projection being maintained, must implement <see cref="IProjection{TKey}" />. </typeparam>
/// <typeparam name="TKey"> The type of key used to identify projections and aggregate roots. </typeparam>
/// <typeparam name="TEvent"> The specific event type this handler processes, must implement <see cref="IEventStoreMessage{TKey}" />. </typeparam>
/// <remarks>
/// This adapter is essential for the event sourcing pattern implementation, allowing strongly-typed event handlers to participate in the
/// projection pipeline while maintaining type safety. The adapter handles the casting and delegation to the underlying handler implementation.
/// </remarks>
public sealed class ProjectionEventHandlerAdapter<TProjection, TKey, TEvent>(ProjectionEventHandlerBase<TProjection, TKey, TEvent> handler)
#pragma warning restore CA1005 // Avoid excessive parameters on generic types
	: IProjectionHandler
	where TProjection : IProjection<TKey>
	where TKey : notnull
	where TEvent : IEventStoreMessage<TKey>
{
	/// <summary>
	/// Asynchronously handles the processing of an event message by casting it to the appropriate type and delegating to the underlying
	/// projection handler for processing.
	/// </summary>
	/// <param name="message"> The event message to process, which will be cast to type <typeparamref name="TEvent" />. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during event processing. </param>
	/// <returns> A task that represents the asynchronous event handling operation. </returns>
	/// <exception cref="InvalidCastException"> Thrown when the message cannot be cast to <typeparamref name="TEvent" />. </exception>
	/// <remarks>
	/// This method performs type-safe casting of the incoming message and coordinates the projection update process by resolving matching
	/// projections and applying the event to each one. The trimming suppression ensures that AOT compilation preserves necessary type information.
	/// </remarks>
	[UnconditionalSuppressMessage("Trimming", "IL2060:Mapping of type casting",
		Justification = "TEvent type is preserved through generic constraints and registration")]
	public async Task HandleAsync(object message, CancellationToken cancellationToken)
	{
		var typedEvent = (TEvent)message;
		var projections = await handler.ResolveMatchingProjectionsAsync(typedEvent, cancellationToken).ConfigureAwait(false);

		foreach (var projection in projections)
		{
			_ = await handler.ApplyAsync(typedEvent, projection, cancellationToken).ConfigureAwait(false);
		}
	}
}
