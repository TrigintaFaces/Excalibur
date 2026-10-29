// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics.CodeAnalysis;
using Excalibur.EventSourcing;


namespace Excalibur.Data.DynamoDb.Projections;

public sealed partial class DynamoDbProjectionStore<TProjection>
{
    internal DynamoDbProjectionStore(DynamoDbProjectionStore<TProjection> source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var parts = destination.Split('/');
        if (parts.Length != 3 || parts[0] != "dynamodb" || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            throw new ArgumentException("Expected an exact dynamodb destination identity.", nameof(destination));
        }

        _client = source._client;
        _scopeCache = source._scopeCache;
        _logger = source._logger;
        _projectionType = DynamoDbProjectionDestinationIdentity.DecodeSegment(parts[2]);
        _jsonOptions = source._jsonOptions;
        _options = new DynamoDbProjectionStoreOptions
        {
            TableName = DynamoDbProjectionDestinationIdentity.DecodeSegment(parts[1]), PartitionKeyName = source._options.PartitionKeyName,
            AutoCreateTable = false, JsonSerializerOptions = source._options.JsonSerializerOptions,
        };
        _tableVerified = true;
        _destination = new DynamoDbProjectionDestination(_client, _options, EnsureTableAsync, _scopeCache);

    }

}

internal sealed class DynamoDbProjectionDestinationIdentity(string table, string discriminator) : IProjectionDestinationIdentity
{
    public string Identity { get; } = $"dynamodb/{Uri.EscapeDataString(table)}/{Uri.EscapeDataString(discriminator)}";

    internal static string DecodeSegment(string encoded)
    {
        var decoded = Uri.UnescapeDataString(encoded);
        if (string.IsNullOrWhiteSpace(decoded) || !string.Equals(Uri.EscapeDataString(decoded), encoded, StringComparison.Ordinal))
        {
            throw new ArgumentException("A projection destination must contain canonical escaped table and type segments.", nameof(encoded));
        }
        return decoded;
    }

}

internal sealed class DynamoDbGenerationReaderFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TProjection>(DynamoDbProjectionStore<TProjection> source) : IProjectionGenerationReaderFactory<TProjection> where TProjection : class
{
    [RequiresUnreferencedCode("The conventional projection serializer requires reflection.")]
    [RequiresDynamicCode("The conventional projection serializer requires dynamic code.")]
    IProjectionGenerationReader<TProjection> IProjectionGenerationReaderFactory<TProjection>.OpenReader(string destinationIdentity)
    {
        var store = new DynamoDbProjectionStore<TProjection>(source, destinationIdentity);
        return new GenerationReader(store.ResolveScopeAsync, store.GetByIdAsync, store.QueryAsync, store.CountAsync, static () => ValueTask.CompletedTask);
    }

    private sealed class GenerationReader(
        Func<CancellationToken, ValueTask<string>> resolveScope,
        Func<string, CancellationToken, Task<TProjection?>> get,
        Func<IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<IReadOnlyList<TProjection>>> query,
        Func<IDictionary<string, object>?, CancellationToken, Task<long>> count, Func<ValueTask> dispose) : IProjectionGenerationReader<TProjection>
    {
                // The scope the handle has actually resolved. The contract's null means "not resolved yet",
        // never "cannot be established": an unresolvable scope throws from the resolve instead.
        private string? resolved;
        public string? DestinationScope => resolved;
        public async ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken) => resolved = await resolveScope(cancellationToken).ConfigureAwait(false);
        public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) { resolved = await resolveScope(cancellationToken).ConfigureAwait(false); return await get(id, cancellationToken).ConfigureAwait(false); }
        public async Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) { resolved = await resolveScope(cancellationToken).ConfigureAwait(false); return await query(filters, options, cancellationToken).ConfigureAwait(false); }
        public async Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) { resolved = await resolveScope(cancellationToken).ConfigureAwait(false); return await count(filters, cancellationToken).ConfigureAwait(false); }
        public ValueTask DisposeAsync() => dispose();
    }
}
