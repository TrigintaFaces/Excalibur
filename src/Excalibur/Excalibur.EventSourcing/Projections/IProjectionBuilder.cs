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
	IProjectionBuilder<TProjection> WhenHandledBy<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : Excalibur.EventSourcing.Abstractions.IProjectionEventHandler<TProjection, TEvent>;
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
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Assembly scanning uses reflection to discover IProjectionEventHandler<T, TEvent> implementations.")]
	IProjectionBuilder<TProjection> AddProjectionHandlersFromAssembly(System.Reflection.Assembly assembly);
#pragma warning restore RS0016

	/// <summary>
	/// Configures optional caching for ephemeral projection results.
	/// Only applies when the projection is used via <c>IEphemeralProjectionEngine</c>.
	/// </summary>
	/// <param name="ttl">The cache time-to-live.</param>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl);

	/// <summary>
	/// Enables dirty checking to skip persistence when a handler produces no state change.
	/// </summary>
	/// <param name="mode">
	/// The dirty checking strategy. Default is <see cref="DirtyCheckingMode.Equality"/>
	/// which uses <see cref="object.Equals(object?)"/> comparison.
	/// </param>
	/// <returns>This builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// For records (immutable projections), <see cref="DirtyCheckingMode.Equality"/> leverages
	/// the compiler-generated value equality. For mutable projections, ensure <c>Equals</c>
	/// is properly implemented or use <see cref="DirtyCheckingMode.ReferenceEquality"/>.
	/// </para>
	/// </remarks>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	IProjectionBuilder<TProjection> WithDirtyChecking(DirtyCheckingMode mode = DirtyCheckingMode.Equality);
#pragma warning restore RS0016
}
