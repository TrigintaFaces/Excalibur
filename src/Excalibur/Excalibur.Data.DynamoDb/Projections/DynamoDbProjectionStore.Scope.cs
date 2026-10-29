// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Excalibur.EventSourcing;

namespace Excalibur.Data.DynamoDb.Projections;

public sealed partial class DynamoDbProjectionStore<TProjection>
{
    private readonly DynamoDbProjectionDestination _destination;
    private readonly DynamoDbProjectionScopeCache _scopeCache;
    private DynamoDbProjectionDestination Destination => _destination;
    private string RequestTableName => Destination.TableName;
    internal ValueTask<string> ResolveScopeAsync(CancellationToken cancellationToken) => Destination.GetScopeAsync(cancellationToken);
}

internal sealed class DynamoDbProjectionDestination(IAmazonDynamoDB client, DynamoDbProjectionStoreOptions options,
    Func<CancellationToken, Task> ensureTable, DynamoDbProjectionScopeCache cache) : IAsyncProjectionDestinationScope
{
    private ResolvedDestination? _resolvedDestination;
    internal string TableName => Volatile.Read(ref _resolvedDestination)?.TableArn ?? options.TableName;

    public async ValueTask<string> GetScopeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _resolvedDestination) is { } known) { return known.Scope; }
        if (cache.TryGet(options.TableName, out var cached))
        {
            return (Interlocked.CompareExchange(ref _resolvedDestination, cached, null) ?? cached).Scope;
        }
        await ensureTable(cancellationToken).ConfigureAwait(false);
        TableDescription? table = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                table = (await client.DescribeTableAsync(options.TableName, cancellationToken).ConfigureAwait(false)).Table;
                break;
            }
            catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException) when (options.AutoCreateTable && attempt < 4)
            {
                // DynamoDB metadata is eventually consistent immediately after table creation.
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
            }
        }
        var arn = table?.TableArn;
        var parts = arn?.Split(':', 6);
        if (parts is not { Length: 6 } || parts[0] != "arn" || parts[2] != "dynamodb"
            || string.IsNullOrWhiteSpace(parts[3]) || string.IsNullOrWhiteSpace(parts[4])
            || !parts[5].StartsWith("table/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("DynamoDB did not return an authoritative table ARN for projection scope resolution.");
        }
        if (options.TableName.StartsWith("arn:", StringComparison.Ordinal)
            ? !string.Equals(options.TableName, arn, StringComparison.Ordinal)
            : !string.Equals(parts[5], "table/" + options.TableName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("DynamoDB metadata identified a different table than the configured projection destination.");
        }
        var endpoint = client.DetermineServiceOperationEndpoint(new GetItemRequest { TableName = arn });
        if (!Uri.TryCreate(endpoint?.URL, UriKind.Absolute, out var address)
            || !string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidOperationException("DynamoDB did not resolve the actual endpoint for the bound table ARN.");
        }
        var scope = "dynamodb/" + Uri.EscapeDataString(address.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped))
            + "/" + Uri.EscapeDataString(parts[1]) + "/" + Uri.EscapeDataString(parts[3]) + "/" + Uri.EscapeDataString(parts[4]);
        var resolved = cache.GetOrAdd(options.TableName, new ResolvedDestination(arn!, scope));
        // Only a successful resolution is cached. All later requests use its ARN, including when
        // refreshing credentials change the account behind the same client object.
        return (Interlocked.CompareExchange(ref _resolvedDestination, resolved, null) ?? resolved).Scope;
    }

    internal sealed record ResolvedDestination(string TableArn, string Scope);

}

/// <summary>Shares successful metadata across short-lived reader handles of one configured client.</summary>
internal sealed class DynamoDbProjectionScopeCache
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DynamoDbProjectionDestination.ResolvedDestination> _entries = new(StringComparer.Ordinal);

    internal bool TryGet(string table, out DynamoDbProjectionDestination.ResolvedDestination value)
    {
        lock (_gate) { return _entries.TryGetValue(table, out value!); }
    }

    internal DynamoDbProjectionDestination.ResolvedDestination GetOrAdd(string table, DynamoDbProjectionDestination.ResolvedDestination value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(table, out var existing)) { return existing; }
            // Bound retained metadata without invalidating handles that already captured a scope.
            if (_entries.Count >= 128) { _entries.Remove(_entries.Keys.First()); }
            _entries.Add(table, value);
            return value;
        }
    }
}
