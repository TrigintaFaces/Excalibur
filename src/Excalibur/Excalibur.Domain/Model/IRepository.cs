// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Model;

/// <summary>
/// Repository for loading, saving, and deleting entities with CRUD semantics.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
/// <remarks>
/// <para>
/// This interface provides a domain-level repository abstraction for non-event-sourced
/// entities. For event-sourced aggregates, use
/// <c>IEventSourcedRepository&lt;TAggregate, TKey&gt;</c> from
/// <c>Excalibur.EventSourcing.Abstractions</c> instead.
/// </para>
/// <para>
/// <see cref="SaveAsync"/> uses upsert semantics: the implementation determines whether
/// the entity is new or existing based on its <see cref="IEntity{TKey}.Key"/> and
/// performs an insert or update accordingly. This avoids pushing state-tracking
/// concerns onto the consumer.
/// </para>
/// <para>
/// This interface intentionally does not couple to <c>IUnitOfWork</c>. Consumers
/// requiring transactional save-across-repositories should compose with
/// <c>IUnitOfWork</c> from <c>Excalibur.Data.Abstractions</c> at the application layer.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Register and use a CRUD repository
/// public class OrderRepository : IRepository&lt;Order, Guid&gt;
/// {
///     public Task&lt;Order?&gt; GetByIdAsync(Guid id, CancellationToken ct) => ...;
///     public Task SaveAsync(Order entity, CancellationToken ct) => ...;
///     public Task DeleteAsync(Order entity, CancellationToken ct) => ...;
/// }
///
/// // Usage in a handler
/// var order = await repository.GetByIdAsync(orderId, ct);
/// if (order is not null)
/// {
///     order.Cancel();
///     await repository.SaveAsync(order, ct);
/// }
/// </code>
/// </example>
public interface IRepository<TEntity, in TKey>
	where TEntity : class, IEntity<TKey>
	where TKey : notnull
{
	/// <summary>
	/// Gets an entity by its identifier.
	/// </summary>
	/// <param name="id">The entity identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The entity if found; otherwise, <see langword="null"/>.</returns>
	Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken);

	/// <summary>
	/// Saves an entity using upsert semantics (insert if new, update if existing).
	/// </summary>
	/// <param name="entity">The entity to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous save operation.</returns>
	Task SaveAsync(TEntity entity, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an entity.
	/// </summary>
	/// <param name="entity">The entity to delete.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous delete operation.</returns>
	Task DeleteAsync(TEntity entity, CancellationToken cancellationToken);
}
