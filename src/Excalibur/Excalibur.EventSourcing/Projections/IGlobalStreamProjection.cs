// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Defines a projection that processes events from the global event stream
/// across all aggregates and types.
/// </summary>
/// <typeparam name="TState">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// Global stream projections differ from per-aggregate projections in that they
/// receive events from all streams. This enables cross-aggregate views such as
/// dashboards, reporting, and analytics.
/// </para>
/// <para>
/// Implement this interface and register it with a
/// <see cref="GlobalStreamProjectionHost{TState}"/> to process events continuously.
/// </para>
/// </remarks>
public interface IGlobalStreamProjection<TState>
	where TState : class
{
	/// <summary>
	/// Applies a domain event to the projection state.
	/// </summary>
	/// <param name="domainEvent">The domain event to apply.</param>
	/// <param name="state">The current projection state.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task ApplyAsync(IDomainEvent domainEvent, TState state, CancellationToken cancellationToken);
}
