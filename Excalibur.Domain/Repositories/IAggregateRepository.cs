using Excalibur.Domain.Model;

namespace Excalibur.Domain.Repositories;

/// <summary>
///     Represents a repository for managing aggregates of type <typeparamref name="TAggregate" />.
/// </summary>
/// <typeparam name="TAggregate"> The type of the aggregate being managed. Must implement <see cref="IAggregateRoot{TKey}" />. </typeparam>
/// <typeparam name="TKey"> The type of the key used to uniquely identify the aggregate. </typeparam>
public interface IAggregateRepository<TAggregate, in TKey> where TAggregate : class, IAggregateRoot<TKey>
{
	/// <summary>
	///     Deletes an aggregate from the repository.
	/// </summary>
	/// <param name="aggregate"> The aggregate to delete. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> The number of entities deleted. </returns>
	Task<int> Delete(TAggregate aggregate, CancellationToken cancellationToken);

	/// <summary>
	///     Checks whether an aggregate exists in the repository by its key.
	/// </summary>
	/// <param name="key"> The unique identifier of the aggregate. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> <c> true </c> if the aggregate exists; otherwise, <c> false </c>. </returns>
	Task<bool> Exists(TKey key, CancellationToken cancellationToken);

	/// <summary>
	///     Executes a query to retrieve a collection of aggregates matching the specified criteria.
	/// </summary>
	/// <typeparam name="TQuery"> The type of the query used to filter the aggregates. </typeparam>
	/// <param name="query"> The query object containing filtering criteria. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> A collection of aggregates matching the query criteria. </returns>
	Task<IEnumerable<TAggregate>> Query<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>;

	/// <summary>
	///     Executes a query to retrieve a single aggregate matching the specified criteria.
	/// </summary>
	/// <typeparam name="TQuery"> The type of the query used to filter the aggregate. </typeparam>
	/// <param name="query"> The query object containing filtering criteria. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> The aggregate matching the query criteria, or <c> null </c> if no matching aggregate is found. </returns>
	Task<TAggregate?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>;

	/// <summary>
	///     Retrieves an aggregate by its key.
	/// </summary>
	/// <param name="key"> The unique identifier of the aggregate. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> The aggregate associated with the specified key. </returns>
	/// <exception cref="InvalidOperationException"> Thrown if no aggregate is found for the specified key. </exception>
	Task<TAggregate> Read(TKey key, CancellationToken cancellationToken);

	/// <summary>
	///     Saves the specified aggregate to the repository.
	/// </summary>
	/// <param name="aggregate"> The aggregate to save. </param>
	/// <param name="cancellationToken"> A token to monitor for cancellation requests. </param>
	/// <returns> The number of entities saved. </returns>
	Task<int> Save(TAggregate aggregate, CancellationToken cancellationToken);
}
