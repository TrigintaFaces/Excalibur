// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Persists Cosmos DB change-feed continuation tokens so a subscription resumes from where it left
/// off after a process restart, instead of replaying from the beginning of the feed.
/// </summary>
/// <remarks>
/// <para>
/// The pull-model change-feed subscriptions track their continuation token in memory; on restart that
/// progress is lost and the feed is re-read from the configured start position (or the beginning),
/// reprocessing already-handled changes. A durable checkpoint store closes that gap.
/// </para>
/// <para>
/// The default registration (<see cref="InMemoryChangeFeedCheckpointStore"/>) is process-local and
/// therefore preserves the existing non-durable behavior; register a durable implementation (e.g. a
/// Cosmos-container-backed store) to survive restarts. Implementations MUST be safe for concurrent use.
/// </para>
/// </remarks>
public interface IChangeFeedCheckpointStore
{
    /// <summary>
    /// Loads the last persisted continuation token for the given subscription, or
    /// <see langword="null"/> if none has been checkpointed yet.
    /// </summary>
    /// <param name="subscriptionId">A stable identifier for the subscription (lease/processor name).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the load to complete.</param>
    /// <returns>The persisted continuation token, or <see langword="null"/> when absent.</returns>
    Task<string?> LoadAsync(string subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the latest continuation token for the given subscription, overwriting any prior value.
    /// </summary>
    /// <param name="subscriptionId">A stable identifier for the subscription (lease/processor name).</param>
    /// <param name="continuationToken">The continuation token to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the save to complete.</param>
    /// <returns>A task that completes when the checkpoint has been persisted.</returns>
    Task SaveAsync(string subscriptionId, string continuationToken, CancellationToken cancellationToken);
}
