// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;

namespace OutOfBandProjections.Infrastructure;

/// <summary>
/// Simple in-memory implementation of <see cref="IMaterializedViewStore"/> for demonstration.
/// In production, use SqlServer/Postgres/CosmosDb/MongoDB stores.
/// </summary>
public sealed class InMemoryMaterializedViewStore : IMaterializedViewStore
{
    private readonly ConcurrentDictionary<string, byte[]> _views = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _positions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
        where TView : class
    {
        var key = $"{viewName}:{viewId}";
        if (_views.TryGetValue(key, out var data))
        {
            return new ValueTask<TView?>(JsonSerializer.Deserialize<TView>(data));
        }

        return new ValueTask<TView?>((TView?)null);
    }

    /// <inheritdoc />
    public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
        where TView : class
    {
        var key = $"{viewName}:{viewId}";
        _views[key] = JsonSerializer.SerializeToUtf8Bytes(view);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken)
    {
        var key = $"{viewName}:{viewId}";
        _views.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken)
    {
        if (_positions.TryGetValue(viewName, out var position))
        {
            return new ValueTask<long?>((long?)position);
        }

        return new ValueTask<long?>((long?)null);
    }

    /// <inheritdoc />
    public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken)
    {
        _positions[viewName] = position;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets all stored views for display purposes.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> GetAllViews() =>
        new Dictionary<string, byte[]>(_views, StringComparer.Ordinal);
}
