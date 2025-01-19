using System.Diagnostics;
using System.Reflection;

using Excalibur.Data.Outbox;
using Excalibur.DataAccess.Exceptions;
using Excalibur.Domain;
using Excalibur.Domain.Model;
using Excalibur.Domain.Repositories;
using Excalibur.Exceptions;
using Excalibur.Extensions;

namespace Excalibur.Data.Repositories;

/// <summary>
///     Base repository class for managing aggregate roots with CRUD and query operations.
/// </summary>
/// <typeparam name="TAggregate"> The type of the aggregate root. </typeparam>
/// <typeparam name="TKey"> The type of the aggregate key. </typeparam>
/// <typeparam name="TAggregateQuery"> The type of the query object used for querying aggregates. </typeparam>
public abstract class AggregateRepositoryBase<TAggregate, TKey, TAggregateQuery>(IActivityContext context, IOutbox outbox)
	: IAggregateRepository<TAggregate, TKey>
	where TAggregate : class, IAggregateRoot<TKey>
	where TAggregateQuery : class, IAggregateQuery<TAggregate>
{
	/// <summary>
	///     Gets the activity context used for repository operations.
	/// </summary>
	protected IActivityContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

	/// <inheritdoc />
	public async Task<int> Delete(TAggregate aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		try
		{
			var eTag = Context.ETag();

			var rowsAffected = await DeleteInternal(aggregate, eTag, cancellationToken).ConfigureAwait(false);

			if (rowsAffected == 0 && !string.IsNullOrEmpty(eTag))
			{
				throw new ConcurrencyException(aggregate.Key!.ToString()!, TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false));
			}

			_ = await outbox.SaveEventsAsync(aggregate, null, null).ConfigureAwait(false);

			Context.ETag(null);

			return rowsAffected;
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
				nameof(Delete), innerException: ex);
		}
	}

	/// <inheritdoc />
	public async Task<bool> Exists(TKey key, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(key);

		try
		{
			return await ExistsInternal(key, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
				nameof(Exists), innerException: ex);
		}
	}

	/// <inheritdoc />
	public Task<IEnumerable<TAggregate>> Query<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>
	{
		ArgumentNullException.ThrowIfNull(query);

		var queryInternalMethod = GetType().GetMethod(
			nameof(QueryInternal),
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(TQuery), typeof(CancellationToken)],
			null);

		var aggregateQuery = EnforceQueryType(query);

		if (queryInternalMethod != null)
		{
			return (Task<IEnumerable<TAggregate>>)queryInternalMethod.Invoke(this, [aggregateQuery, cancellationToken])!;
		}

		return ReadEnumerable(() => QueryInternal(aggregateQuery, cancellationToken)!);
	}

	/// <inheritdoc />
	public Task<TAggregate?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<TAggregate>
	{
		ArgumentNullException.ThrowIfNull(query);

		var findInternalMethod = GetType().GetMethod(
			nameof(FindInternal),
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(TQuery), typeof(CancellationToken)],
			null);

		var aggregateQuery = EnforceQueryType(query);

		if (findInternalMethod != null)
		{
			return (Task<TAggregate?>)findInternalMethod.Invoke(this, [aggregateQuery, cancellationToken])!;
		}

		return Read(() => FindInternal(aggregateQuery, cancellationToken));
	}

	/// <inheritdoc />
	public Task<TAggregate> Read(TKey key, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(key);
		var result = Read(() => ReadInternal(key, cancellationToken));

		return (result ?? throw new ResourceNotFoundException(key.ToString(), nameof(TAggregate)))!;
	}

	/// <inheritdoc />
	public async Task<int> Save(TAggregate aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		try
		{
			var eTag = Context.ETag();
			var newETag = Uuid7Extensions.GenerateString();

			var rowsAffected = await SaveInternal(aggregate, eTag, newETag, cancellationToken).ConfigureAwait(false);

			if (rowsAffected == 0)
			{
				throw new ConcurrencyException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
					aggregate.Key!.ToString()!);
			}

			_ = await outbox.SaveEventsAsync(aggregate, null, null).ConfigureAwait(false);

			Context.ETag(newETag);

			return rowsAffected;
		}
		catch (Exception ex) when (ex is not ApiException)
		{
			throw new OperationFailedException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
				nameof(Save), innerException: ex);
		}
	}

	protected virtual Task<IEnumerable<TAggregate>> QueryInternal(TAggregateQuery query, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	protected virtual Task<TAggregate?> ReadInternal(TKey key, CancellationToken cancellationToken) => throw new NotImplementedException();

	protected virtual Task<TAggregate?> FindInternal(TAggregateQuery query, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	protected virtual Task<int> DeleteInternal(TAggregate aggregate, string? eTag, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	protected virtual Task<bool> ExistsInternal(TKey key, CancellationToken cancellationToken) => throw new NotImplementedException();

	protected virtual Task<int> SaveInternal(TAggregate aggregate, string eTag, string newETag, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	private static TAggregateQuery EnforceQueryType(IAggregateQuery<TAggregate> query)
	{
		if (query is TAggregateQuery aggregateQuery)
		{
			return aggregateQuery;
		}

		var aggregateName = TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false);
		var queryType = TypeNameHelper.GetTypeDisplayName(query.GetType());

		throw new InvalidOperationException($"The repository for '{aggregateName}' does not support '{queryType}'.");
	}

	private async Task<TAggregate?> Read(Func<Task<TAggregate?>> readOperation)
	{
		try
		{
			var result = await readOperation().ConfigureAwait(false);

			Context.ETag(result?.ETag);

			return result;
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
				nameof(Query), innerException: ex);
		}
	}

	private async Task<IEnumerable<TAggregate>> ReadEnumerable(Func<Task<IEnumerable<TAggregate>?>> readOperation)
	{
		try
		{
			var result = await readOperation().ConfigureAwait(false);

			var enumeration = result?.ToArray() ?? Enumerable.Empty<TAggregate>().ToArray();

			var eTags = enumeration.Where(x => !string.IsNullOrEmpty(x.ETag)).Select(x => $"{x.Key}:{x.ETag}");

			Context.ETag(string.Join(',', eTags));

			return enumeration;
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(TypeNameHelper.GetTypeDisplayName(typeof(TAggregate), false, false),
				nameof(Query), innerException: ex);
		}
	}
}
