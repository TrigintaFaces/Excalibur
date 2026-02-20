// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.EventSourced;

/// <summary>
/// Provides event-sourced persistence for saga state.
/// </summary>
/// <remarks>
/// <para>
/// Instead of storing the current saga state directly, this store persists
/// individual saga events and rehydrates the state by replaying them.
/// This provides a complete audit trail and enables temporal queries.
/// </para>
/// <para>
/// Follows the <c>Excalibur.EventSourcing.IEventStore</c> pattern with
/// a minimal interface surface (2 methods) focused on saga-specific operations.
/// </para>
/// </remarks>
public interface IEventSourcedSagaStore
{
	/// <summary>
	/// Appends a saga event to the event stream for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="sagaEvent">The saga event to append.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sagaId"/> is null or empty.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="sagaEvent"/> is null.
	/// </exception>
	Task AppendEventAsync(string sagaId, ISagaEvent sagaEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Rehydrates the saga state by replaying all events for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga instance identifier.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The rehydrated saga state if events exist for the specified saga;
	/// otherwise, <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sagaId"/> is null or empty.
	/// </exception>
	Task<SagaState?> RehydrateAsync(string sagaId, CancellationToken cancellationToken);
}
