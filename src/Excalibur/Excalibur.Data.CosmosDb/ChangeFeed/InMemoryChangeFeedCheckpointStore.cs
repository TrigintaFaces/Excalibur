// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.CosmosDb.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Process-local <see cref="IChangeFeedCheckpointStore"/> backed by a concurrent dictionary.
/// </summary>
/// <remarks>
/// This is the default registration. It is NOT durable across process restarts — it preserves the
/// pre-existing non-durable change-feed behavior so the durable-continuation feature is strictly
/// opt-in: register a persistent <see cref="IChangeFeedCheckpointStore"/> (e.g. Cosmos-container-backed)
/// to actually survive restarts (bd-egwtku). Safe for concurrent use.
/// <para>
/// Because shipping a *silently* non-durable default would re-create the advertised-but-inert bug the
/// durable feature fixes (bd-ydln24), this store emits a LOUD <see cref="LogLevel.Warning"/> once on
/// construction naming the consequence and the remedy
/// (<c>AddCosmosDbChangeFeedCheckpointStore</c>). When a durable store is registered it replaces this
/// default, so this store is never constructed and the warning never fires.
/// </para>
/// </remarks>
internal sealed partial class InMemoryChangeFeedCheckpointStore : IChangeFeedCheckpointStore
{
    private readonly ConcurrentDictionary<string, string> _checkpoints = new(StringComparer.Ordinal);
    private readonly ILogger<InMemoryChangeFeedCheckpointStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryChangeFeedCheckpointStore"/> class and emits a
    /// once-on-construction non-durable warning (the store is the registered fallback when no durable
    /// checkpoint store is configured).
    /// </summary>
    /// <param name="logger">
    /// The logger used to emit the non-durable warning. Optional so direct (non-DI) construction stays
    /// NRE-safe; defaults to <see cref="NullLogger{T}.Instance"/> (fail-open). The DI activator injects the
    /// real <see cref="ILogger{TCategoryName}"/> so the warning is observable on the configured pipeline.
    /// </param>
    public InMemoryChangeFeedCheckpointStore(ILogger<InMemoryChangeFeedCheckpointStore>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryChangeFeedCheckpointStore>.Instance;
        LogNonDurableContinuation();
    }

    /// <inheritdoc />
    public Task<string?> LoadAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_checkpoints.TryGetValue(subscriptionId, out var token) ? token : null);
    }

    /// <inheritdoc />
    public Task SaveAsync(string subscriptionId, string continuationToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        ArgumentNullException.ThrowIfNull(continuationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _checkpoints[subscriptionId] = continuationToken;
        return Task.CompletedTask;
    }

    [LoggerMessage(
        DataCosmosDbEventId.ChangeFeedDurabilityNonDurableWarning,
        LogLevel.Warning,
        "Change-feed continuation is NON-DURABLE (in-memory default) — checkpoints are lost on restart and "
        + "changes may be re-read from the beginning. Call AddCosmosDbChangeFeedCheckpointStore(...) to "
        + "enable durable continuation. (bd-egwtku/ajt1iy)")]
    private partial void LogNonDurableContinuation();
}
