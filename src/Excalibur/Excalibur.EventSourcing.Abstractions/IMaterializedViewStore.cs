// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for materialized view persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// Materialized views are read-optimized projections built from event streams.
/// This interface provides the storage operations for view state.
/// </para>
/// <para>
/// For building views from events, use <see cref="IMaterializedViewBuilder{TView}"/>.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for synchronous completions (e.g., in-memory stores, cache hits). Callers should await the result
/// immediately and not store the ValueTask for later use.
/// </para>
/// </remarks>
public interface IMaterializedViewStore
{
	/// <summary>
	/// Gets a materialized view by its identifier.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The view if found, or null if not found.</returns>
	ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
		where TView : class;

	/// <summary>
	/// Saves a materialized view.
	/// </summary>
	/// <typeparam name="TView">The view type.</typeparam>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="view">The view to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
		where TView : class;

	/// <summary>
	/// Deletes a materialized view.
	/// </summary>
	/// <param name="viewName">The view name (typically the view type name).</param>
	/// <param name="viewId">The view identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the last processed position for a view.
	/// </summary>
	/// <param name="viewName">The view name for position tracking.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The last processed position, or null if no position has been recorded.</returns>
	/// <remarks>
	/// <para>
	/// The position is used to track which events have been processed for a view,
	/// enabling catch-up subscriptions and rebuild scenarios.
	/// </para>
	/// </remarks>
	ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken);

	/// <summary>
	/// Saves the last processed position for a view.
	/// </summary>
	/// <param name="viewName">The view name for position tracking.</param>
	/// <param name="position">The position to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken);
}
