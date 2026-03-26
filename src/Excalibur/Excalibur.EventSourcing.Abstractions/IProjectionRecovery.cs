// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Provides recovery for inline projections that failed after events were committed.
/// </summary>
/// <remarks>
/// <para>
/// This service is optional in DI. Register it only if you use inline projections
/// and want programmatic recovery instead of relying solely on async catch-up.
/// </para>
/// <para>
/// <see cref="ReapplyAsync{TProjection}"/> loads all events from the event store
/// for the given aggregate and applies them through the registered <c>When&lt;T&gt;</c>
/// handlers, then persists the result. It does NOT re-append events.
/// </para>
/// </remarks>
public interface IProjectionRecovery
{
	/// <summary>
	/// Re-applies all events for the specified aggregate to the projection, replacing
	/// the current projection state. Does not re-append events to the event store.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to recover.</typeparam>
	/// <param name="aggregateId">The aggregate identifier whose projection failed.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous recovery operation.</returns>
	Task ReapplyAsync<TProjection>(
		string aggregateId,
		CancellationToken cancellationToken)
		where TProjection : class, new();
}
