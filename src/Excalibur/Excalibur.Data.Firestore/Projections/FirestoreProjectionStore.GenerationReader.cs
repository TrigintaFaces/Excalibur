// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;
using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore.Projections;

public sealed partial class FirestoreProjectionStore<TProjection>
{
    private readonly CollectionReference? _generationCollection;
    private FirestoreProjectionStore(FirestoreProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        _db = source._db;
        _options = source._options;
        _projectionType = source._projectionType;
        _jsonOptions = source._jsonOptions;
        var sourcePath = source.GetCollection().Path;
        var prefix = "firestore/" + sourcePath[..(sourcePath.IndexOf("/documents/", StringComparison.Ordinal) + "/documents/".Length)];
        if (!destination.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("The reader must address the configured Firestore project and database.", nameof(destination));
        }
        _generationCollection = _db.Collection(destination[prefix.Length..]);

    }

    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new FirestoreProjectionStore<TProjection>(this, destinationIdentity);
        return new GenerationReader((store as IServiceProvider).GetService(typeof(IProjectionDestinationScope)) is IProjectionDestinationScope scope ? scope.Scope : null, store.GetByIdAsync, store.QueryAsync, store.CountAsync, static () => ValueTask.CompletedTask);
    }

    private sealed class GenerationReader(
        string? destinationScope,
        Func<string, CancellationToken, Task<TProjection?>> get,
        Func<IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<IReadOnlyList<TProjection>>> query,
        Func<IDictionary<string, object>?, CancellationToken, Task<long>> count, Func<ValueTask> dispose) : IProjectionGenerationReader<TProjection>
    {
        public string? DestinationScope => destinationScope;
        public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) => get(id, cancellationToken);
        public Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) => query(filters, options, cancellationToken);
        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) => count(filters, cancellationToken);
        public ValueTask DisposeAsync() => dispose();
    }
}
