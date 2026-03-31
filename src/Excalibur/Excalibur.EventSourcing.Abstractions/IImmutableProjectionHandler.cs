// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines a typed, DI-resolved event handler for immutable projection transforms.
/// </summary>
/// <typeparam name="TProjection">The immutable projection state type (e.g., a C# record).</typeparam>
/// <typeparam name="TEvent">The domain event type this handler processes.</typeparam>
/// <remarks>
/// <para>
/// Unlike <see cref="IProjectionEventHandler{TProjection, TEvent}"/> which mutates state
/// in-place, this handler returns a new projection instance (functional transform pattern).
/// </para>
/// <para>
/// The <c>current</c> parameter on <see cref="TransformAsync"/> is <see langword="null"/>
/// when this is the first event for a projection ID. Handlers should create a new
/// instance (factory) or throw depending on their semantics.
/// </para>
/// </remarks>
public interface IImmutableProjectionHandler<TProjection, in TEvent>
	where TProjection : class
	where TEvent : IDomainEvent
{
	/// <summary>
	/// Transforms the projection state by producing a new instance from the current
	/// state and the event.
	/// </summary>
	/// <param name="current">
	/// The current projection state, or <see langword="null"/> if no projection exists
	/// for this ID yet (creation scenario).
	/// </param>
	/// <param name="event">The domain event to process.</param>
	/// <param name="context">
	/// Context providing aggregate metadata and optional projection ID override.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A new projection instance representing the updated state.</returns>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	Task<TProjection> TransformAsync(
		TProjection? current,
		TEvent @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken);
#pragma warning restore RS0016
}
