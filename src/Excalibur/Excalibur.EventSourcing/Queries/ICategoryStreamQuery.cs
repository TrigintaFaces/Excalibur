// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Queries events across all streams within a category (aggregate type).
/// </summary>
/// <remarks>
/// <para>
/// Categories correspond to aggregate types. For example, all <c>Order</c> aggregates
/// belong to the <c>Order</c> category. This interface enables cross-aggregate queries
/// within a single category, useful for:
/// <list type="bullet">
/// <item>Building per-category projections</item>
/// <item>Category-scoped event feeds</item>
/// <item>Analytics per aggregate type</item>
/// </list>
/// </para>
/// <para>
/// Follows the pattern from EventStoreDB's <c>$ce-{category}</c> system projection.
/// The existing <see cref="Abstractions.IEventStore"/> already captures <c>aggregateType</c>
/// on append, so category information is inherently available in the store.
/// </para>
/// </remarks>
public interface ICategoryStreamQuery
{
	/// <summary>
	/// Reads events for all aggregates of a given category, starting at the specified position.
	/// </summary>
	/// <param name="category">The category (aggregate type) to query.</param>
	/// <param name="fromPosition">The position to start reading from.</param>
	/// <param name="maxCount">The maximum number of events to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events in the category in commit order.</returns>
	Task<IReadOnlyList<StoredEvent>> ReadCategoryAsync(
		string category,
		long fromPosition,
		int maxCount,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the list of known categories (aggregate types) in the event store.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The distinct category names.</returns>
	Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);
}
