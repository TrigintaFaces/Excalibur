// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

// CA1005: The three type parameters are necessary for type-safe event sourcing projections: TProjection - The projection type, TKey - The
// aggregate/projection key type, TEvent - The specific event type These cannot be reduced without losing compile-time type safety in the
// event handling pipeline.
// R0.8: Avoid excessive parameters on generic types
#pragma warning disable CA1005

/// <summary>
/// Provides the base implementation for strongly-typed projection event handlers in the event sourcing system. This abstract class defines
/// the contract for implementing domain-specific projection logic while maintaining type safety and enabling polymorphic behavior through
/// the projection handling infrastructure.
/// </summary>
/// <typeparam name="TProjection"> The type of projection being maintained, must implement <see cref="IProjection{TKey}" />. </typeparam>
/// <typeparam name="TKey"> The type of key used to identify projections and aggregate roots. </typeparam>
/// <typeparam name="TEvent"> The specific event type this handler processes. </typeparam>
/// <remarks>
/// Derived classes must implement the projection resolution and event application logic. This base class serves as the foundation for the
/// Command Query Responsibility Segregation (CQRS) pattern implementation, enabling separate read models optimized for specific query scenarios.
/// </remarks>
public abstract class ProjectionEventHandlerBase<TProjection, TKey, TEvent>
#pragma warning restore CA1005 // Avoid excessive parameters on generic types
	where TProjection : IProjection<TKey>
	where TKey : notnull
{
	/// <summary>
	/// Asynchronously resolves the collection of projections that should be updated in response to the given event. This method determines
	/// which specific projection instances need to be modified based on the event content and business logic requirements.
	/// </summary>
	/// <param name="evt"> The event that triggered the projection update process. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during projection resolution. </param>
	/// <returns>
	/// A task that represents the asynchronous operation, containing a collection of projections that should be updated in response to the event.
	/// </returns>
	/// <remarks>
	/// Implementations should consider performance implications when resolving projections, potentially using caching or optimized queries
	/// to minimize database roundtrips. The method may return an empty collection if no projections match the event criteria.
	/// </remarks>
	public abstract Task<IEnumerable<TProjection>> ResolveMatchingProjectionsAsync(TEvent evt, CancellationToken cancellationToken);

	/// <summary>
	/// Asynchronously applies the specified event to a projection instance, updating the projection's state to reflect the business changes
	/// represented by the event.
	/// </summary>
	/// <param name="evt"> The event containing the state changes to apply to the projection. </param>
	/// <param name="projection"> The projection instance to be updated with the event data. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during event application. </param>
	/// <returns> A task that represents the asynchronous operation, containing the updated projection instance. </returns>
	/// <remarks>
	/// This method encapsulates the domain-specific logic for transforming events into projection updates. Implementations should ensure
	/// idempotency where possible and handle concurrent modifications appropriately. The returned projection should reflect all changes
	/// from the applied event.
	/// </remarks>
	public abstract Task<TProjection> ApplyAsync(TEvent evt, TProjection projection, CancellationToken cancellationToken);
}
