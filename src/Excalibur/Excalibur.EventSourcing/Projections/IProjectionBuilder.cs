// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// Configures optional caching for ephemeral projection results.
	/// Only applies when the projection is used via <c>IEphemeralProjectionEngine</c>.
	/// </summary>
	/// <param name="ttl">The cache time-to-live.</param>
	/// <returns>This builder for fluent chaining.</returns>
	IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl);
}
