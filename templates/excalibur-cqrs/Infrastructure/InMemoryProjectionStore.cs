using System.Collections.Concurrent;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.Infrastructure;

/// <summary>
/// In-memory implementation of <see cref="IProjectionStore{TProjection}"/> for demonstration purposes.
/// </summary>
/// <remarks>
/// In a real application, replace this with a database-backed projection store
/// (e.g., SQL Server, PostgreSQL, CosmosDB, Elasticsearch).
/// </remarks>
/// <typeparam name="TProjection">The projection type.</typeparam>
public sealed class InMemoryProjectionStore<TProjection> : IProjectionStore<TProjection>
    where TProjection : class
{
    private readonly ConcurrentDictionary<string, TProjection> _store = new();

    /// <inheritdoc />
    public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        _store.TryGetValue(id, out var projection);
        return Task.FromResult(projection);
    }

    /// <inheritdoc />
    public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
    {
        _store[id] = projection;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TProjection>> QueryAsync(
        IDictionary<string, object>? filters,
        QueryOptions? options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TProjection> results = _store.Values.ToList();
        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
        => Task.FromResult((long)_store.Count);
}
