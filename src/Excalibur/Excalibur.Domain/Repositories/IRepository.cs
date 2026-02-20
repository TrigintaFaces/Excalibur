// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Domain.Repositories;

/// <summary>
/// Simple domain repository pattern for CRUD operations on entities.
/// This is NOT event-sourced; for event-sourced aggregates use
/// <c>IEventSourcedRepository</c> from <c>Excalibur.EventSourcing</c>.
/// </summary>
/// <typeparam name="T">The entity type to manage. Must have a string identifier.</typeparam>
/// <remarks>
/// <para>
/// Follows the <c>IDistributedCache</c> pattern from Microsoft.Extensions.Caching:
/// minimal interface (4 methods), focused on essential operations.
/// </para>
/// <para>
/// For advanced query scenarios, use the <c>IDataRequest</c> pattern from
/// <c>Excalibur.Data.Abstractions</c> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderRepository : IRepository&lt;Order&gt;
/// {
///     public Task&lt;Order?&gt; GetByIdAsync(string id, CancellationToken ct)
///         => _db.ExecuteAsync(new GetOrderByIdRequest(id), ct);
/// }
/// </code>
/// </example>
public interface IRepository<T> where T : class
{
	/// <summary>
	/// Retrieves an entity by its identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the entity.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>The entity if found; otherwise, <see langword="null"/>.</returns>
	Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken);

	/// <summary>
	/// Persists an entity (insert or update).
	/// </summary>
	/// <param name="entity">The entity to save.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the entity is saved.</returns>
	Task SaveAsync(T entity, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an entity by its identifier.
	/// </summary>
	/// <param name="id">The unique identifier of the entity to delete.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns><see langword="true"/> if the entity was deleted; <see langword="false"/> if not found.</returns>
	Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether an entity with the specified identifier exists.
	/// </summary>
	/// <param name="id">The unique identifier to check.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns><see langword="true"/> if the entity exists; otherwise, <see langword="false"/>.</returns>
	Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);
}
