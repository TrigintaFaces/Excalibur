// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Re-folds one aggregate's projection row from its event stream, on demand.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseEventNotification</c> registers this service, so a host that registers projections at
/// all has it; call <c>UseProjectionRecovery</c> directly only to get recovery without the
/// notification pipeline. It is not merely a convenience: erasing an aggregate re-folds the
/// affected projection rows through this service, so a container without it fails that path.
/// </para>
/// <para>
/// A row that is simply behind needs nothing from this: the projection host applies committed
/// events in global-stream order and does not advance past a batch whose apply faulted, so it
/// retries that batch itself. Reach for recovery to repair one aggregate's row on demand
/// instead of rebuilding the whole projection.
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
	/// <param name="aggregateType">The aggregate type name (required for event store stream lookup).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous recovery operation.</returns>
	Task ReapplyAsync<TProjection>(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TProjection : class, new();
}
