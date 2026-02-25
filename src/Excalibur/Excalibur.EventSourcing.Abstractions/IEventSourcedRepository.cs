// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Repository for loading and saving event-sourced aggregates with generic key support.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <typeparam name="TKey">The type of the aggregate identifier.</typeparam>
/// <remarks>
/// <para>
/// This interface provides full event sourcing repository operations including:
/// <list type="bullet">
/// <item>CRUD operations with strongly-typed keys</item>
/// <item>ETag-based optimistic concurrency control</item>
/// <item>Query support via <see cref="IAggregateQuery{TAggregate}"/></item>
/// <item>Soft delete via tombstone events</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderRepository : IEventSourcedRepository&lt;OrderAggregate, Guid&gt;
/// {
///     // Implementation
/// }
///
/// // Usage
/// var order = await repository.GetByIdAsync(orderId, ct);
/// await repository.SaveAsync(order, order.ETag, ct);
/// </code>
/// </example>
public interface IEventSourcedRepository<TAggregate, TKey>
	where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
	where TKey : notnull
{
	/// <summary>
	/// Gets an aggregate by its strongly-typed identifier.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The aggregate if found, otherwise <see langword="null"/>.</returns>
	[RequiresUnreferencedCode("Aggregate rehydration may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate rehydration may require dynamic code generation.")]
	Task<TAggregate?> GetByIdAsync(TKey aggregateId, CancellationToken cancellationToken);

	/// <summary>
	/// Saves an aggregate and its uncommitted events.
	/// </summary>
	/// <param name="aggregate">The aggregate to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
	Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken);

	/// <summary>
	/// Saves an aggregate with ETag-based optimistic concurrency check.
	/// </summary>
	/// <param name="aggregate">The aggregate to save.</param>
	/// <param name="expectedETag">
	/// The expected ETag value. If the current ETag doesn't match,
	/// a <see cref="Dispatch.Messaging.Exceptions.ConcurrencyException"/> is thrown.
	/// Pass <see langword="null"/> to skip ETag validation.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	/// <exception cref="ConcurrencyException">
	/// Thrown when the expected ETag doesn't match the current aggregate ETag.
	/// </exception>
	[RequiresUnreferencedCode("Aggregate persistence may require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("Aggregate persistence may require dynamic code generation.")]
	Task SaveAsync(TAggregate aggregate, string? expectedETag, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if an aggregate exists.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the aggregate exists; otherwise, <see langword="false"/>.</returns>
	Task<bool> ExistsAsync(TKey aggregateId, CancellationToken cancellationToken);

	/// <summary>
	/// Soft-deletes an aggregate by appending a tombstone event.
	/// </summary>
	/// <param name="aggregate">The aggregate to delete.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous delete operation.</returns>
	/// <remarks>
	/// This performs a soft delete by marking the aggregate as deleted.
	/// The aggregate's event history is preserved for audit purposes.
	/// </remarks>
	Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken);

	/// <summary>
	/// Executes a query to find multiple aggregates matching the criteria.
	/// </summary>
	/// <typeparam name="TQuery">The query type.</typeparam>
	/// <param name="query">The query object containing search criteria.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A collection of aggregates matching the query.</returns>
	/// <remarks>
	/// Query execution depends on the underlying event store's query capabilities.
	/// Some stores may require projections for efficient querying.
	/// </remarks>
	Task<IReadOnlyList<TAggregate>> QueryAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>;

	/// <summary>
	/// Executes a query to find a single aggregate matching the criteria.
	/// </summary>
	/// <typeparam name="TQuery">The query type.</typeparam>
	/// <param name="query">The query object containing search criteria.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The first aggregate matching the query, or <see langword="null"/> if not found.</returns>
	Task<TAggregate?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>;
}

/// <summary>
/// Repository for loading and saving event-sourced aggregates with string identifiers.
/// </summary>
/// <typeparam name="TAggregate">The aggregate type.</typeparam>
/// <remarks>
/// <para>
/// This is a convenience interface for aggregates using string identifiers.
/// For other key types, use <see cref="IEventSourcedRepository{TAggregate, TKey}"/>.
/// </para>
/// </remarks>
public interface IEventSourcedRepository<TAggregate> : IEventSourcedRepository<TAggregate, string>
	where TAggregate : class, IAggregateRoot<string>, IAggregateSnapshotSupport
{
}
