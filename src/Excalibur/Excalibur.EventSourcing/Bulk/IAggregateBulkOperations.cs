// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Bulk;

/// <summary>
/// Provides batch aggregate operations for loading and saving multiple aggregates efficiently.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// <para>
/// Batch operations can significantly reduce round-trips to the event store when
/// working with multiple aggregates. Implementations may optimize by batching
/// database queries or parallelizing operations.
/// </para>
/// <para>
/// Follows the minimal interface pattern (2 methods) from Microsoft design guidelines.
/// </para>
/// </remarks>
public interface IAggregateBulkOperations<TAggregate, TKey>
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
{
	/// <summary>
	/// Loads multiple aggregates by their identifiers.
	/// </summary>
	/// <param name="aggregateIds">The aggregate identifiers to load.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A dictionary mapping aggregate identifiers to their loaded instances.
	/// Aggregates that do not exist are omitted from the result.
	/// </returns>
	Task<IReadOnlyDictionary<TKey, TAggregate>> LoadManyAsync(
		IEnumerable<TKey> aggregateIds,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves multiple aggregates and their uncommitted events.
	/// </summary>
	/// <param name="aggregates">The aggregates to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A bulk save result indicating the outcome for each aggregate.
	/// </returns>
	Task<BulkSaveResult<TKey>> SaveManyAsync(
		IEnumerable<TAggregate> aggregates,
		CancellationToken cancellationToken);
}

/// <summary>
/// Represents the result of a bulk save operation.
/// </summary>
/// <typeparam name="TKey">The aggregate key type.</typeparam>
/// <param name="SuccessCount">The number of aggregates saved successfully.</param>
/// <param name="FailureCount">The number of aggregates that failed to save.</param>
/// <param name="Failures">Details of any failures.</param>
public sealed record BulkSaveResult<TKey>(
	int SuccessCount,
	int FailureCount,
	IReadOnlyList<BulkSaveFailure<TKey>> Failures)
	where TKey : notnull
{
	/// <summary>
	/// Gets a value indicating whether all aggregates were saved successfully.
	/// </summary>
	/// <value><see langword="true"/> if all saves succeeded; otherwise, <see langword="false"/>.</value>
	public bool AllSucceeded => FailureCount == 0;
}

/// <summary>
/// Represents a failure in a bulk save operation for a specific aggregate.
/// </summary>
/// <typeparam name="TKey">The aggregate key type.</typeparam>
/// <param name="AggregateId">The identifier of the aggregate that failed.</param>
/// <param name="Exception">The exception that occurred.</param>
public sealed record BulkSaveFailure<TKey>(
	TKey AggregateId,
	Exception Exception)
	where TKey : notnull;
