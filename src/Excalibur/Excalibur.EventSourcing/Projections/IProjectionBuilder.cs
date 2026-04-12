// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Fluent builder for configuring a projection's mode, event handlers, and optional caching.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// Use this builder within <c>AddProjection&lt;T&gt;</c> to configure how a projection
/// processes events:
/// </para>
/// <code>
/// builder.AddProjection&lt;OrderSummary&gt;(p => p
///     .Inline()
///     .When&lt;OrderPlaced&gt;((proj, e) => { proj.Total = e.Amount; })
///     .When&lt;OrderShipped&gt;((proj, e) => { proj.ShippedAt = e.ShippedAt; }));
/// </code>
/// <para>
/// If neither <see cref="Inline"/> nor <see cref="Async"/> is called, the projection
/// defaults to <see cref="ProjectionMode.Async"/> (R27.31).
/// </para>
/// <para>
/// A second registration for the same projection type replaces the first (R27.37).
/// </para>
/// </remarks>
public interface IProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	/// <summary>
	/// Configures the projection to run inline during <c>SaveAsync</c>,
	/// providing immediate read-after-write consistency.
	/// </summary>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> Inline();

	/// <summary>
	/// Configures the projection to run asynchronously via the
	/// <c>GlobalStreamProjectionHost</c>. This is the default mode.
	/// </summary>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> Async();

	/// <summary>
	/// Configures the projection as ephemeral — built on-demand without persistence.
	/// Ephemeral projections are never stored; they are rebuilt from events each time
	/// they are requested via <c>IEphemeralProjectionEngine</c>.
	/// </summary>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> Ephemeral();

	/// <summary>
	/// Registers an event handler for the specified domain event type.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type to handle.</typeparam>
	/// <param name="handler">
	/// An action that applies the event to the projection state.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent;

#pragma warning restore RS0016

	/// <summary>
	/// Registers a DI-resolved typed event handler for the specified domain event type.
	/// </summary>
	/// <typeparam name="TEvent">The domain event type to handle.</typeparam>
	/// <typeparam name="THandler">
	/// The handler type implementing
	/// <see cref="Excalibur.EventSourcing.Abstractions.IProjectionEventHandler{TProjection, TEvent}"/>.
	/// </typeparam>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// The handler is resolved from DI per invocation, enabling constructor injection
	/// for cross-aggregate lookups, logging, and other services.
	/// </para>
	/// <para>
	/// A second registration for the same event type replaces the first (R27.37).
	/// </para>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> WhenHandledBy<TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
		THandler>()
		where TEvent : IDomainEvent
		where THandler : IProjectionEventHandler<TProjection, TEvent>;

#pragma warning restore RS0016

	/// <summary>
	/// Scans the specified assembly for all implementations of
	/// <see cref="Excalibur.EventSourcing.Abstractions.IProjectionEventHandler{TProjection, TEvent}"/>
	/// and registers them as handlers for this projection.
	/// </summary>
	/// <param name="assembly">The assembly to scan.</param>
	/// <returns>This builder for fluent chaining.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if two handler classes handle the same (TProjection, TEvent) pair.
	/// </exception>
	/// <remarks>
	/// <para>
	/// If the assembly contains no matching handler implementations, this is a no-op (D7).
	/// </para>
	/// <para>
	/// Assembly scanning discovers handlers at startup. For AOT scenarios,
	/// prefer explicit <c>WhenHandledBy</c> registration instead.
	/// </para>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	[RequiresUnreferencedCode(
		"Assembly scanning uses reflection to discover IProjectionEventHandler<T, TEvent> implementations.")]
	IProjectionBuilder<TProjection> AddProjectionHandlersFromAssembly(System.Reflection.Assembly assembly);

#pragma warning restore RS0016

	/// <summary>
	/// Registers a key derivation function for the specified event type.
	/// When an inline projection processes an event of this type, the projection
	/// instance is loaded and stored using the derived key instead of the aggregate ID.
	/// This enables multi-stream projections keyed by event data (e.g., category, tenant, date).
	/// </summary>
	/// <typeparam name="TEvent">The domain event type to extract the key from.</typeparam>
	/// <param name="keySelector">
	/// A function that extracts the projection key from the event.
	/// Must return a non-null, non-empty string.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> KeyedBy<TEvent>(Func<TEvent, string> keySelector)
		where TEvent : IDomainEvent;
#pragma warning restore RS0016

	/// <summary>
	/// Configures optional caching for ephemeral projection results.
	/// Only applies when the projection is used via <c>IEphemeralProjectionEngine</c>.
	/// </summary>
	/// <param name="ttl">The cache time-to-live.</param>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl);

	/// <summary>
	/// Registers a deletion handler that is invoked when the associated aggregate
	/// is deleted (R27.23). The handler receives the projection ID and a cancellation token,
	/// and is responsible for removing the projection from its store.
	/// </summary>
	/// <param name="deleteAction">
	/// An async function that deletes the projection by its ID.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Only one deletion handler may be registered per projection. If called multiple times,
	/// the last handler wins.
	/// </para>
	/// <code>
	/// builder.AddProjection&lt;OrderSummary&gt;(p => p
	///     .Inline()
	///     .When&lt;OrderPlaced&gt;((proj, e) => { /* ... */ })
	///     .WhenDeleted((projectionId, ct) => store.DeleteAsync(projectionId, ct)));
	/// </code>
	/// </remarks>
	IProjectionBuilder<TProjection> WhenDeleted(Func<string, CancellationToken, Task> deleteAction);

	/// <summary>
	/// Registers an identity resolver for the specified event type.
	/// When processing an event of this type, the projection instance is loaded
	/// and stored using the resolved identity instead of the aggregate ID.
	/// This enables projections keyed by event data (e.g., order ID, tenant, category).
	/// </summary>
	/// <typeparam name="TEvent">The domain event type to extract the identity from.</typeparam>
	/// <param name="resolver">
	/// A function that extracts the projection identity from the event.
	/// Must return a non-null, non-empty string.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// If no identity resolver is provided, <c>IDomainEvent.AggregateId</c> is used (default).
	/// </para>
	/// <code>
	/// builder.AddProjection&lt;OrderSummary&gt;(p => p
	///     .Inline()
	///     .IdentityFrom&lt;OrderPlaced&gt;(e => e.OrderId.ToString())
	///     .When&lt;OrderPlaced&gt;((proj, e) => { /* ... */ }));
	/// </code>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> IdentityFrom<TEvent>(Func<TEvent, string> resolver)
		where TEvent : IDomainEvent;
#pragma warning restore RS0016

	/// <summary>
	/// Overrides the default DI-resolved <see cref="Excalibur.EventSourcing.Abstractions.IProjectionStore{TProjection}"/>
	/// with a specific store implementation type. The store is resolved from DI by its concrete type.
	/// </summary>
	/// <typeparam name="TStore">
	/// The concrete store implementation type. Must implement
	/// <see cref="Excalibur.EventSourcing.Abstractions.IProjectionStore{TProjection}"/>.
	/// </typeparam>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// If no store is explicitly bound, the framework resolves
	/// <c>IProjectionStore&lt;TProjection&gt;</c> from DI (default keyed registration).
	/// </para>
	/// <code>
	/// builder.AddProjection&lt;OrderSummary&gt;(p => p
	///     .Inline()
	///     .WithStore&lt;SqlServerProjectionStore&lt;OrderSummary&gt;&gt;()
	///     .When&lt;OrderPlaced&gt;((proj, e) => { /* ... */ }));
	/// </code>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> WithStore<TStore>()
		where TStore : class, IProjectionStore<TProjection>;
#pragma warning restore RS0016

	/// <summary>
	/// Configures per-projection options such as warning thresholds.
	/// </summary>
	/// <param name="configure">An action that configures <see cref="ProjectionOptions"/>.</param>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <code>
	/// builder.AddProjection&lt;OrderSummary&gt;(p => p
	///     .Inline()
	///     .WithOptions(o => o.WarningThreshold = TimeSpan.FromMilliseconds(200))
	///     .When&lt;OrderPlaced&gt;((proj, e) => { /* ... */ }));
	/// </code>
	/// </remarks>
	IProjectionBuilder<TProjection> WithOptions(Action<ProjectionOptions> configure);

}
