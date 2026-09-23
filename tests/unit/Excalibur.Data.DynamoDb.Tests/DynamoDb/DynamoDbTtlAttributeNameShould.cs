// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Domain.Model;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Locks that the snapshot and saga stores write an item's expiry under the CONFIGURED
/// <c>TtlAttributeName</c> -- the same option the stores hand to <c>UpdateTimeToLive</c> as the table's TTL
/// attribute.
/// </summary>
/// <remarks>
/// <para>
/// Both stores enabled TTL on <c>TtlAttributeName</c> while their document writers wrote the expiry under a
/// fixed <c>"ttl"</c>. DynamoDB expires an item only by the attribute its TTL specification names, so once the
/// option was renamed the table watched an attribute no item carried and nothing ever expired -- silent,
/// unbounded retention of data the consumer had configured to be deleted.
/// </para>
/// <para>
/// Driven through the public <c>Save*</c> path with a faked client, so the assertion is on the item the store
/// actually sends, not on a helper. LIVENESS arms prove the default name still carries the expiry, so a writer
/// that dropped the TTL altogether cannot pass.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbTtlAttributeNameShould
{
	private const int TtlSeconds = 3600;

	[Fact]
	public async Task SnapshotStore_WritesTheExpiryUnderTheConfiguredTtlAttribute()
	{
		var item = await SaveSnapshotAndCaptureItemAsync("expiresAt");

		AssertExpiryUnder(item, "expiresAt");
		item.ShouldNotContainKey("ttl", "the expiry must not be written under a name the table's TTL does not watch");
	}

	[Fact]
	public async Task SnapshotStore_WritesTheExpiryUnderTheDefaultTtlAttribute()
	{
		var item = await SaveSnapshotAndCaptureItemAsync(ttlAttributeName: null);

		AssertExpiryUnder(item, "ttl");
	}

	[Fact]
	public async Task SagaStore_WritesTheExpiryUnderTheConfiguredTtlAttribute()
	{
		var item = await SaveSagaAndCaptureItemAsync("expiresAt");

		AssertExpiryUnder(item, "expiresAt");
		item.ShouldNotContainKey("ttl", "the expiry must not be written under a name the table's TTL does not watch");
	}

	[Fact]
	public async Task SagaStore_WritesTheExpiryUnderTheDefaultTtlAttribute()
	{
		var item = await SaveSagaAndCaptureItemAsync(ttlAttributeName: null);

		AssertExpiryUnder(item, "ttl");
	}

	private static void AssertExpiryUnder(Dictionary<string, AttributeValue> item, string attribute)
	{
		item.ShouldContainKey(attribute, $"the expiry must be written under the configured TTL attribute '{attribute}'");

		var expiresAt = long.Parse(item[attribute].N, System.Globalization.CultureInfo.InvariantCulture);
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// A future epoch-seconds value within the configured window, so a stray attribute of the same name
		// carrying anything else (a zero, a timestamp string) does not satisfy the arm.
		expiresAt.ShouldBeGreaterThan(now);
		expiresAt.ShouldBeLessThanOrEqualTo(now + TtlSeconds + 60);
	}

	private static async Task<Dictionary<string, AttributeValue>> SaveSnapshotAndCaptureItemAsync(string? ttlAttributeName)
	{
		PutItemRequest? captured = null;
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.GetItemAsync(A<GetItemRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new GetItemResponse { Item = [] }));
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.ReturnsLazily((PutItemRequest r, CancellationToken _) =>
			{
				captured = r;
				return Task.FromResult(new PutItemResponse());
			});

		var options = new DynamoDbSnapshotStoreOptions
		{
			TableName = "snapshots",
			CreateTableIfNotExists = false,
			DefaultTtlSeconds = TtlSeconds,
		};
		if (ttlAttributeName is not null)
		{
			options.TtlAttributeName = ttlAttributeName;
		}

		var store = new DynamoDbSnapshotStore(
			client,
			Options.Create(options),
			A.Fake<ILogger<DynamoDbSnapshotStore>>(),
			TestTenantContext.Untenanted);

		await store.SaveSnapshotAsync(
			new Snapshot
			{
				SnapshotId = Guid.NewGuid().ToString(),
				AggregateId = "aggregate-1",
				AggregateType = "Order",
				Version = 3,
				CreatedAt = DateTimeOffset.UtcNow,
				Data = new byte[] { 1, 2, 3 },
			},
			CancellationToken.None);

		captured.ShouldNotBeNull("the store must have written the snapshot");
		return captured.Item;
	}

	private static async Task<Dictionary<string, AttributeValue>> SaveSagaAndCaptureItemAsync(string? ttlAttributeName)
	{
		PutItemRequest? captured = null;
		var client = A.Fake<IAmazonDynamoDB>();
		_ = A.CallTo(() => client.GetItemAsync(A<GetItemRequest>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new GetItemResponse { Item = [] }));
		_ = A.CallTo(() => client.PutItemAsync(A<PutItemRequest>._, A<CancellationToken>._))
			.ReturnsLazily((PutItemRequest r, CancellationToken _) =>
			{
				captured = r;
				return Task.FromResult(new PutItemResponse());
			});

		var options = new DynamoDbSagaOptions
		{
			TableName = "sagas",
			CreateTableIfNotExists = false,
			DefaultTtlSeconds = TtlSeconds,
		};
		if (ttlAttributeName is not null)
		{
			options.TtlAttributeName = ttlAttributeName;
		}

		var store = new DynamoDbSagaStore(
			client,
			Options.Create(options),
			A.Fake<ILogger<DynamoDbSagaStore>>(),
			new DispatchJsonSerializer(),
			tenantContext: TestTenantContext.Untenanted);

		// Version 1 takes the update leg of the save, which issues the PutItem directly; the create leg first
		// probes for legacy items, which is not what this lock is about.
		await store.SaveAsync(new TtlProbeSagaState { Version = 1 }, CancellationToken.None);

		captured.ShouldNotBeNull("the store must have written the saga");
		return captured.Item;
	}

	private sealed class TtlProbeSagaState : SagaState;
}
