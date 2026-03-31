// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines a typed, DI-resolved event handler for projection updates.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <typeparam name="TEvent">The domain event type this handler processes.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface for projection logic that requires dependency injection,
/// async operations, or custom projection ID targeting. Register via
/// <c>WhenHandledBy&lt;TEvent, THandler&gt;()</c> on the projection builder.
/// </para>
/// <para>
/// For simple synchronous projections, prefer <c>When&lt;TEvent&gt;</c> lambdas instead.
/// </para>
/// </remarks>
public interface IProjectionEventHandler<TProjection, in TEvent>
	where TProjection : class, new()
	where TEvent : IDomainEvent
{
	/// <summary>
	/// Applies the event to the projection state.
	/// </summary>
	/// <param name="projection">The projection instance to update.</param>
	/// <param name="event">The domain event to process.</param>
	/// <param name="context">
	/// Context providing aggregate metadata and optional projection ID override.
	/// Set <see cref="ProjectionHandlerContext.OverrideProjectionId"/> to target
	/// a different projection instance than the default (aggregate ID).
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
#pragma warning disable RS0016 // Add public types and members to the declared API (constrained generic not representable in baseline)
	Task HandleAsync(
		TProjection projection,
		TEvent @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken);
#pragma warning restore RS0016
}
