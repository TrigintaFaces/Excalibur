// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// fgfhbo sibling — real-Firestore (emulator) lock proving the cloud-native outbox converges on ONE
/// representation of "no tenant": the reserved <c>__untenanted__</c> sentinel, never an absent field.
/// </summary>
/// <remarks>
/// <para>
/// <c>FirestoreOutboxStore.ToFirestoreDocument</c> wrote the <c>tenantId</c> field only when non-empty,
/// and <c>FromFirestoreDocument</c> read it back only when present — an untenanted message round-tripped
/// as <see langword="null"/>, the identical shape as the Redis and DynamoDb outbox defects. RED against
/// that pre-fix shape.
/// </para>
/// <para>
/// TTL here (<c>DefaultTimeToLiveSeconds</c>) is applied only at publish time and never at write time, so
/// an unpublished document carries no TTL. No migration of already-persisted documents is required
/// regardless: the read path folds a MISSING field the same way it folds an explicit sentinel
/// (<c>KeyedTenantPartition.FromStoredValue</c>), so a legacy document and a freshly-written untenanted
/// document are indistinguishable to every reader, forever.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously
/// (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Firestore")]
[Collection(FirestoreOutboxTestCollection.CollectionName)]
public sealed class FirestoreOutboxStoreUntenantedSentinelShould : IAsyncLifetime
{
	private const string Sentinel = "__untenanted__";

	private readonly FirestoreOutboxStoreContainerFixture _fixture;
	private readonly List<(FirestoreOutboxStore Store, string CollectionName)> _created = [];

	public FirestoreOutboxStoreUntenantedSentinelShould(FirestoreOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var (store, collectionName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			await _fixture.CleanupCollectionAsync(collectionName).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: an untenanted message reads back the reserved sentinel — not null, not an absent field.
	/// </summary>
	[Fact]
	public async Task AddAnUntenantedMessage_AndReadBackTheSentinel()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(tenantId: null, partitionKeyValue: partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		addResult.Success.ShouldBeTrue($"add must succeed: {addResult.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 100, CancellationToken.None).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, message.MessageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("the untenanted message must round-trip");
		own.TenantId.ShouldBe(
			Sentinel,
			"an untenanted message must bind the reserved sentinel — a null readback means the field was "
			+ "omitted on write, the pre-fix defect.");
	}

	/// <summary>
	/// SAFETY: a real tenant is stored verbatim — the sentinel conversion never absorbs a real tenant id.
	/// </summary>
	[Fact]
	public async Task AddARealTenantedMessage_AndReadBackTheRealTenantVerbatim()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		const string tenantId = "acme-corp";
		var message = CreateMessage(tenantId: tenantId, partitionKeyValue: partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		addResult.Success.ShouldBeTrue($"add must succeed: {addResult.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 100, CancellationToken.None).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, message.MessageId, StringComparison.Ordinal));

		own.ShouldNotBeNull();
		own.TenantId.ShouldBe(tenantId, "a real tenant must survive the write unchanged");
	}

	/// <summary>
	/// Read-tolerance: a document written directly (bypassing this store — the pre-fix shape) carries no
	/// <c>tenantId</c> field at all. The fixed read path must fold that absence onto the sentinel, not
	/// surface it as <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task ReadALegacyDocumentMissingTheTenantField_AsTheSentinel()
	{
		var (store, collectionName) = await CreateStoreWithDetailsAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		const string messageId = "legacy-msg-1";

		// Write directly via the fixture's raw Db client, bypassing FirestoreOutboxStore entirely,
		// replicating the PRE-FIX shape: no tenantId field present at all.
		var doc = new Dictionary<string, object>
		{
			["messageId"] = messageId,
			["partitionKey"] = partitionKey.Value,
			["messageType"] = "TestMessageType",
			["payload"] = Convert.ToBase64String("legacy-payload"u8.ToArray()),
			["createdAt"] = DateTimeOffset.UtcNow.ToString("o"),
			["isPublished"] = false,
			["retryCount"] = 0
			// Deliberately NO tenantId field — the pre-fix legacy shape.
		};
		_ = await _fixture.Db.Collection(collectionName).Document(messageId)
			.SetAsync(doc, cancellationToken: CancellationToken.None).ConfigureAwait(false);

		var pending = await store.GetPendingAsync(partitionKey, 100, CancellationToken.None).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("a legacy document missing the tenantId field must still round-trip");
		own.TenantId.ShouldBe(
			Sentinel,
			"an absent tenantId field must fold onto the sentinel on read, the same as an explicit "
			+ "null/empty/sentinel — a legacy document and a freshly-written untenanted document must "
			+ "read back identically.");
	}

	private static CloudOutboxMessage CreateMessage(string? tenantId, string partitionKeyValue) =>
		new()
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			TenantId = tenantId,
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKeyValue
		};

	private async Task<FirestoreOutboxStore> CreateStoreAsync()
	{
		var (store, _) = await CreateStoreWithDetailsAsync().ConfigureAwait(false);
		return store;
	}

	private async Task<(FirestoreOutboxStore Store, string CollectionName)> CreateStoreWithDetailsAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("The Firestore emulator must be available — never skipped.");

		var collection = $"outbox_{Guid.NewGuid():N}";
		var options = new FirestoreOutboxOptions
		{
			ProjectId = _fixture.ProjectId,
			EmulatorHost = _fixture.EmulatorHost,
			CollectionName = collection,
			CreateCollectionIfNotExists = false
		};

		var store = new FirestoreOutboxStore(Options.Create(options), NullLogger<FirestoreOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, collection));
		return (store, collection);
	}
}
