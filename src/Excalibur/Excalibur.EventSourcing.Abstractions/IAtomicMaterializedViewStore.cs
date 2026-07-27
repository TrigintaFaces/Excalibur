// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing;

/// <summary>
/// A <see cref="IMaterializedViewStore"/> that can persist a view and advance its processed position as one
/// durable, all-or-nothing operation — the precondition for exactly-once projection.
/// </summary>
/// <remarks>
/// <para>
/// This capability is a separate type, not a virtual member on <see cref="IMaterializedViewStore"/>, because
/// a store cannot honour it by inheriting a default. Writing the view and the position as two operations and
/// crashing between them replays the event on restart, which double-counts any view that accumulates rather
/// than overwrites. A store that has no way to commit both writes together must therefore be unable to
/// express the guarantee at all, rather than silently inherit it.
/// </para>
/// <para>
/// Not every backing store can implement this. A store whose view and position live in different indices or
/// collections with no cross-document transaction cannot commit both, and does not implement this interface.
/// Such a store is rejected when a projection requiring exactly-once is wired to it, at startup.
/// </para>
/// <para>
/// Implementing this interface is necessary but not sufficient: a store whose atomicity depends on runtime
/// configuration must report that configuration through <see cref="SupportsAtomicWrites"/>, so a deployment
/// that disables it is refused at startup rather than degrading to at-least-once in production.
/// </para>
/// </remarks>
public interface IAtomicMaterializedViewStore : IMaterializedViewStore
{
	/// <summary>
	/// Gets a value indicating whether this store instance can currently perform atomic view+position writes.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when <see cref="SaveViewAndPositionAsync{TView}"/> commits both writes as one
	/// unit; otherwise <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Implementing the interface states the store is *capable* of atomic writes; this property states whether
	/// the current configuration *enables* them. A store whose atomicity is unconditional returns
	/// <see langword="true"/> constantly. A store whose atomicity requires an opt-in — a transaction mode, a
	/// replica set — returns the value of that setting, so the check reads the option that actually governs
	/// the behaviour rather than a proxy for it.
	/// </remarks>
	bool SupportsAtomicWrites { get; }

	/// <summary>
	/// Persists a view and advances the processed position for its view name in a single durable operation.
	/// </summary>
	/// <typeparam name="TView">The materialized view type.</typeparam>
	/// <param name="viewName">The logical view name the position belongs to.</param>
	/// <param name="viewId">The identifier of the view instance being written.</param>
	/// <param name="view">The view to persist.</param>
	/// <param name="position">The global stream position that produced <paramref name="view"/>.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <remarks>
	/// Both writes commit, or neither does. The position advance is monotonic: a lower position never
	/// overwrites a higher one, so a delayed or retried write cannot rewind the checkpoint and replay events
	/// that were already applied.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="viewName"/> or <paramref name="viewId"/> is <see langword="null"/>, empty, or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
	ValueTask SaveViewAndPositionAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		long position,
		CancellationToken cancellationToken)
		where TView : class;
}
