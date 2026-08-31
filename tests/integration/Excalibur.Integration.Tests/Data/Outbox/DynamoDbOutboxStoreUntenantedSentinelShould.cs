// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// fgfhbo sibling — real-DynamoDB (LocalStack) lock proving the cloud-native outbox converges on ONE
/// representation of "no tenant": the reserved <c>__untenanted__</c> sentinel, never an absent attribute.
/// </summary>
/// <remarks>
/// <para>
/// <c>DynamoDbOutboxStore.ToAttributeMap</c> wrote the <c>tenantId</c> attribute only when non-empty, and
/// <c>FromAttributeMap</c> read it back only when present — an untenanted message round-tripped as
/// <see langword="null"/>, the identical shape as the Redis outbox defect (fgfhbo) and the SQL providers'
/// pre-migration state. RED against that pre-fix shape.
/// </para>
/// <para>
/// TTL here is applied only at <c>MarkAsPublishedAsync</c> (<c>DefaultTimeToLiveSeconds</c>, optional —
/// 0 disables it) and never at write time, so an unpublished row carries no TTL and is not
/// time-bounded. No migration of already-persisted rows is required regardless: the read path folds a
/// MISSING attribute the same way it folds an explicit sentinel (<c>KeyedTenantPartition.FromStoredValue</c>),
/// so a legacy row and a freshly-written untenanted row are indistinguishable to every reader, forever —
/// not merely until a TTL window elapses.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously
/// (<c>verify-against-real-infra-not-mock</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbOutboxStoreUntenantedSentinelShould
	: IClassFixture<DynamoDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private const string Sentinel = "__untenanted__";

	private readonly DynamoDbOutboxStoreContainerFixture _fixture;
	private readonly List<(DynamoDbOutboxStore Store, string TableName)> _created = [];

	public DynamoDbOutboxStoreUntenantedSentinelShould(DynamoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var (store, tableName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: an untenanted message reads back the reserved sentinel — not null, not an absent attribute.
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
			"an untenanted message must bind the reserved sentinel — a null readback means the attribute "
			+ "was omitted on write, the pre-fix defect.");
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
	/// Read-tolerance: a row written directly (bypassing this store — the pre-fix shape, or any writer
	/// on an older package version) carries no <c>tenantId</c> attribute at all. The fixed read path must
	/// fold that absence onto the sentinel, not surface it as <see langword="null"/>.
	/// </summary>
	[Fact]
	public async Task ReadALegacyItemMissingTheTenantAttribute_AsTheSentinel()
	{
		var (store, tableName, options) = await CreateStoreWithDetailsAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		const string messageId = "legacy-msg-1";

		// Write directly via the fixture's raw client, bypassing DynamoDbOutboxStore entirely, replicating
		// the PRE-FIX shape: no tenantId attribute present at all.
		await _fixture.Client.PutItemAsync(
			tableName,
			new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
			{
				[options.PartitionKeyAttribute] = new() { S = partitionKey.Value },
				[options.SortKeyAttribute] = new() { S = messageId },
				["messageType"] = new() { S = "TestMessageType" },
				["payload"] = new() { S = Convert.ToBase64String("legacy-payload"u8.ToArray()) },
				["createdAt"] = new() { S = DateTimeOffset.UtcNow.ToString("o") },
				["isPublished"] = new() { BOOL = false },
				["retryCount"] = new() { N = "0" }
				// Deliberately NO tenantId attribute — the pre-fix legacy shape.
			},
			CancellationToken.None).ConfigureAwait(false);

		var pending = await store.GetPendingAsync(partitionKey, 100, CancellationToken.None).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, messageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("a legacy item missing the tenantId attribute must still round-trip");
		own.TenantId.ShouldBe(
			Sentinel,
			"an absent tenantId attribute must fold onto the sentinel on read, the same as an explicit "
			+ "null/empty/sentinel — a legacy item and a freshly-written untenanted item must read back "
			+ "identically.");
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

	private async Task<DynamoDbOutboxStore> CreateStoreAsync()
	{
		var (store, _, _) = await CreateStoreWithDetailsAsync().ConfigureAwait(false);
		return store;
	}

	private async Task<(DynamoDbOutboxStore Store, string TableName, DynamoDbOutboxOptions Options)> CreateStoreWithDetailsAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("LocalStack DynamoDB must be available — never skipped.");

		var table = $"outbox_{Guid.NewGuid():N}";
		var opts = new DynamoDbOutboxOptions
		{
			TableName = table,
			CreateTableIfNotExists = true,
			EnableStreams = false,
			Connection = new DynamoDbOutboxConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				AccessKey = "test",
				SecretKey = "test"
			}
		};

		var store = new DynamoDbOutboxStore(Options.Create(opts), NullLogger<DynamoDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, table));
		return (store, table, opts);
	}
}
