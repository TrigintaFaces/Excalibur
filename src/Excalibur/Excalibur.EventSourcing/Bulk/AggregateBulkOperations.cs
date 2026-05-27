// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Bulk;

/// <summary>
/// Default implementation of <see cref="IAggregateBulkOperations{TAggregate,TKey}"/>
/// that delegates to <see cref="IEventSourcedRepository{TAggregate,TKey}"/>.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// <para>
/// This implementation loads and saves aggregates sequentially via the repository.
/// Provider-specific implementations can override this behavior to use batched
/// database operations for better performance.
/// </para>
/// </remarks>
public sealed class AggregateBulkOperations<TAggregate, TKey> : IAggregateBulkOperations<TAggregate, TKey>
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
{
	private readonly IEventSourcedRepository<TAggregate, TKey> _repository;

	/// <summary>
	/// Initializes a new instance of the <see cref="AggregateBulkOperations{TAggregate,TKey}"/> class.
	/// </summary>
	/// <param name="repository">The event-sourced repository.</param>
	public AggregateBulkOperations(IEventSourcedRepository<TAggregate, TKey> repository)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Aggregate rehydration delegates to EventSourcedRepository which handles type preservation.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Aggregate rehydration delegates to EventSourcedRepository which handles dynamic code.")]
	public async Task<IReadOnlyDictionary<TKey, TAggregate>> LoadManyAsync(
		IEnumerable<TKey> aggregateIds,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregateIds);

		var result = new Dictionary<TKey, TAggregate>();

		foreach (var id in aggregateIds)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var aggregate = await _repository.GetByIdAsync(id, cancellationToken)
				.ConfigureAwait(false);

			if (aggregate is not null)
			{
				result[id] = aggregate;
			}
		}

		return result;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage("AOT", "IL2026",
		Justification = "Aggregate persistence delegates to EventSourcedRepository which handles type preservation.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Aggregate persistence delegates to EventSourcedRepository which handles dynamic code.")]
	public async Task<BulkSaveResult<TKey>> SaveManyAsync(
		IEnumerable<TAggregate> aggregates,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregates);

		var successCount = 0;
		var failures = new List<BulkSaveFailure<TKey>>();

		foreach (var aggregate in aggregates)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				await _repository.SaveAsync(aggregate, cancellationToken)
					.ConfigureAwait(false);
				successCount++;
			}
			catch (Exception ex)
			{
				failures.Add(new BulkSaveFailure<TKey>(aggregate.Id, ex));
			}
		}

		return new BulkSaveResult<TKey>(successCount, failures.Count, failures);
	}
}
