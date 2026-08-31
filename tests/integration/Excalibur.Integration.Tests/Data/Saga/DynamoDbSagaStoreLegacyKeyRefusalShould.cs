// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.DynamoDb;
using Excalibur.Saga.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds the refusal that stands between an unmigrated DynamoDB saga table and a silently restarted saga:
/// an item written under the pre-tenant partition key is unaddressable now, so a load of the saga it holds
/// reports NO SAGA IN FLIGHT - which the caller reads as a saga that has not begun, so it starts the saga
/// over and re-fires every compensating action and external call that has already happened. On the create
/// path the same silence lets the <c>attribute_not_exists</c> guard SUCCEED, so a second, duplicate saga is
/// written beside the original.
/// </summary>
/// <remarks>
/// <para>
/// The tenant segment is NOT the leading one on this provider - the partition key reads
/// <c>SAGA#t:{tenant}:{sagaId}</c>, so the item-kind discriminator comes first. A probe copied from a
/// provider whose tenant segment leads would match no key at all and report every table clean; negated, it
/// would match every key and report every table dirty. Both arms below therefore also serve as evidence
/// that the composed prefix is the one being tested.
/// </para>
/// <para>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed item and requires an
/// absent saga to load as <see langword="null"/> and a new saga to then be creatable. That reaches the
/// probe rather than bypassing it - an absent saga is exactly what triggers it - so the arm proves the
/// probe comes back clean, not merely that it never ran.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbSagaStoreLegacyKeyRefusalShould
	: IClassFixture<DynamoDbSagaStoreContainerFixture>
{
	private const string TenantA = "tenant-A";

	private readonly DynamoDbSagaStoreContainerFixture _fixture;

	// One table per test instance. xUnit builds a fresh instance per arm, so neither arm can observe what
	// the other seeded.
	private readonly string _tableName = "saga_legacy_key_" + Guid.NewGuid().ToString("N");

	public DynamoDbSagaStoreLegacyKeyRefusalShould(DynamoDbSagaStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY: a table still holding an item written without a tenant segment is refused, by name, before it
	/// can be read back as "no saga in flight".
	/// </summary>
	[Fact]
	public async Task Refuse_a_table_holding_an_item_written_without_a_tenant_segment()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"LocalStack DynamoDB must be available - this arm exists to prove a real table is refused, so it "
			+ "is never skipped");

		await ProvisionTableAsync().ConfigureAwait(false);

		// The shape an earlier release wrote on this provider: the item-kind discriminator and the saga
		// identifier, with no tenant segment between them.
		var legacySagaId = Guid.NewGuid();
		var legacyPartitionKey = $"SAGA#{legacySagaId}";
		await SeedItemAsync(legacyPartitionKey, legacySagaId).ConfigureAwait(false);

		var loadRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.LoadAsync<TestSagaState>(legacySagaId, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		loadRefusal.Message.ShouldContain(
			_tableName,
			Case.Sensitive,
			"the refusal must name the table a consumer has to re-key, or it cannot be acted on");

		loadRefusal.Message.ShouldContain(
			legacyPartitionKey,
			Case.Sensitive,
			"naming the offending key is what lets a consumer confirm which items are affected");

		// The create path is guarded separately, and it is the one that produces a DUPLICATE saga rather
		// than a restarted one: attribute_not_exists is evaluated against the new partition key, so the
		// legacy item does not fail the condition and the second create succeeds.
		var createRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.SaveAsync(
					new TestSagaState { SagaId = Guid.NewGuid(), TenantId = TenantA },
					CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		createRefusal.Message.ShouldContain(
			_tableName,
			Case.Sensitive,
			"a create must refuse for the same reason a load does, and name the same table");

		// The refusal is a refusal, not a repair: the item is still exactly where it was, and the refused
		// create wrote nothing beside it.
		(await ScanItemCountAsync().ConfigureAwait(false)).ShouldBe(
			1,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the "
			+ "data - and the refused create must not have written its own item");
	}

	/// <summary>
	/// LIVENESS: a table whose items all carry a tenant segment is served normally. Without this arm a probe
	/// that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_table_whose_items_all_carry_a_tenant_segment()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"LocalStack DynamoDB must be available - a correctly-keyed table must remain fully usable, so "
			+ "this arm is never skipped");

		// Written through the store, so the seeded item carries exactly the partition key this release
		// composes rather than one the test invented. This is also the empty-table case: a brand-new
		// deployment must not be refused.
		await CreateStore(TenantA)
			.SaveAsync(new TestSagaState { SagaId = Guid.NewGuid(), TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		// A fresh instance, so its probe has not already run: the load below is its first absence decision.
		var store = CreateStore(TenantA);

		var loaded = await store
			.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		loaded.ShouldBeNull("a saga that was never started must load as null, not refuse");

		// An absent read proves less than a write does: the store must actually remain usable.
		var newSagaId = Guid.NewGuid();
		await store
			.SaveAsync(new TestSagaState { SagaId = newSagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var reloaded = await store
			.LoadAsync<TestSagaState>(newSagaId, CancellationToken.None)
			.ConfigureAwait(false);

		_ = reloaded.ShouldNotBeNull("a correctly-keyed table must remain fully writable");
	}

	private DynamoDbSagaStore CreateStore(string? tenantId) =>
		new(
			Options.Create(new DynamoDbSagaOptions
			{
				Connection = new DynamoDbConnectionOptions
				{
					ServiceUrl = _fixture.ServiceUrl,
					Region = "us-east-1",
					AccessKey = "test",
					SecretKey = "test",
				},
				TableName = _tableName,
				CreateTableIfNotExists = true,
				UseConsistentReads = true,
			}),
			NullLogger<DynamoDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	// A short-lived client per call rather than a field: the fixture exposes only the endpoint, and a
	// per-call client keeps this test free of a disposal contract it would otherwise have to carry.
	private AmazonDynamoDBClient RawClient() =>
		new(
			new BasicAWSCredentials("test", "test"),
			new AmazonDynamoDBConfig { ServiceURL = _fixture.ServiceUrl });

	/// <summary>
	/// Creates the table through the store itself, which is the only thing in the system that knows the key
	/// schema. The load it performs is the empty-table arm of the probe: it must not refuse.
	/// </summary>
	private async Task ProvisionTableAsync()
	{
		var loaded = await CreateStore(TenantA)
			.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		loaded.ShouldBeNull("a newly provisioned, empty table holds nothing to refuse");
	}

	// Seeded through the raw client rather than through the store, because the store can no longer write the
	// shape under test - that is the whole point of the change this locks.
	private async Task SeedItemAsync(string partitionKey, Guid sagaId)
	{
		using var client = RawClient();

		_ = await client.PutItemAsync(
			_tableName,
			new Dictionary<string, AttributeValue>
			{
				["PK"] = new AttributeValue { S = partitionKey },
				["SK"] = new AttributeValue { S = nameof(TestSagaState) },
				["sagaId"] = new AttributeValue { S = sagaId.ToString() },
				["sagaType"] = new AttributeValue { S = nameof(TestSagaState) },
				["stateJson"] = new AttributeValue { S = "{}" },
				["isCompleted"] = new AttributeValue { BOOL = false },
				["version"] = new AttributeValue { N = "1" },
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	private async Task<int> ScanItemCountAsync()
	{
		using var client = RawClient();

		var response = await client.ScanAsync(
			new ScanRequest { TableName = _tableName },
			CancellationToken.None).ConfigureAwait(false);

		return response.Items?.Count ?? 0;
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
