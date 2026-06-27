// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json.Serialization;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Durable <see cref="IChangeFeedCheckpointStore"/> that persists change-feed continuation tokens as
/// documents in a Cosmos DB container, so a subscription resumes across process restarts.
/// </summary>
/// <remarks>
/// <para>
/// One document per subscription (id = subscription id, partition key path <c>/subscriptionId</c>),
/// upserted after each processed batch and read on start. Mirrors the persistence pattern of
/// <c>CosmosDbCdcStateStore</c> (bd-egwtku). Register this in place of the default
/// <see cref="InMemoryChangeFeedCheckpointStore"/> to get durable continuation.
/// </para>
/// <para>
/// The caller supplies the target <see cref="Container"/> (a dedicated checkpoints container or a shared
/// one with the <c>/subscriptionId</c> partition key path); this type does not create the container.
/// </para>
/// </remarks>
internal sealed class CosmosDbChangeFeedCheckpointStore : IChangeFeedCheckpointStore
{
    private readonly Container _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbChangeFeedCheckpointStore"/> class.
    /// </summary>
    /// <param name="container">The Cosmos DB container that stores checkpoint documents.</param>
    public CosmosDbChangeFeedCheckpointStore(Container container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <inheritdoc />
    public async Task<string?> LoadAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);

        try
        {
            var response = await _container.ReadItemAsync<CheckpointDocument>(
                subscriptionId,
                new PartitionKey(subscriptionId),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return string.IsNullOrEmpty(response.Resource?.ContinuationToken)
                ? null
                : response.Resource.ContinuationToken;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No checkpoint persisted yet — start from the configured position.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string subscriptionId, string continuationToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriptionId);
        ArgumentNullException.ThrowIfNull(continuationToken);

        var document = new CheckpointDocument
        {
            Id = subscriptionId,
            SubscriptionId = subscriptionId,
            ContinuationToken = continuationToken,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _ = await _container.UpsertItemAsync(
            document,
            new PartitionKey(subscriptionId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal document structure for a persisted change-feed checkpoint. Every property carries BOTH a
    /// <see cref="JsonPropertyNameAttribute"/> (System.Text.Json) and a
    /// <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> with the same lowercase name, so the document
    /// serializes deterministically regardless of the DI-supplied <see cref="CosmosClient"/>'s serializer.
    /// This is load-bearing: the Cosmos SDK v3 <b>default serializer is Newtonsoft</b> (System.Text.Json is
    /// opt-in), which ignores STJ attributes — so an STJ-only annotation would emit PascalCase
    /// <c>"Id"</c>/<c>"SubscriptionId"</c>, leaving the Cosmos-required system key <c>id</c> absent and the
    /// document's partition-key field mismatched against the <c>/subscriptionId</c> path. The result is a
    /// point-read that never finds the checkpoint, so continuation silently resumes from the beginning every
    /// restart (durable continuation inert). bd-ydln24 / bd-i2eabb.
    /// </summary>
    private sealed class CheckpointDocument
    {
        /// <summary>Gets or sets the document id (matches the subscription id; Cosmos-required lowercase <c>id</c>).</summary>
        [JsonPropertyName("id")]
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the subscription id (partition key, path <c>/subscriptionId</c>).</summary>
        [JsonPropertyName("subscriptionId")]
        [Newtonsoft.Json.JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Gets or sets the persisted change-feed continuation token.</summary>
        [JsonPropertyName("continuationToken")]
        [Newtonsoft.Json.JsonProperty("continuationToken")]
        public string ContinuationToken { get; set; } = string.Empty;

        /// <summary>Gets or sets when this checkpoint was last updated.</summary>
        [JsonPropertyName("updatedAt")]
        [Newtonsoft.Json.JsonProperty("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
