// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="DynamoDbInboxStore"/> using the Inbox Conformance
/// Test Kit against a LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// The store is constructed with the fixture's <em>default-config</em> DynamoDB client (no custom
/// serializers), so the suite exercises the real wire behaviour a default consumer client produces — the
/// conditional-write first-writer-wins dedup (<c>attribute_not_exists</c>), the
/// <c>ConditionalCheckFailedException</c> status transitions, and the item -> attribute-map round-trip a
/// mocked client could never reproduce. The real-infra suite is never skipped.
/// </para>
/// <para>
/// The store auto-creates its table: the injected-client constructor preserves the supplied client and
/// <see cref="DynamoDbInboxStore.InitializeAsync"/> runs the <c>CreateTableIfNotExists</c> path, so this
/// deriver exercises the real consumer-supplied-client auto-create flow (no fixture table pre-create);
/// cleanup deletes the per-test table.
/// </para>
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<DynamoDbInboxStoreContainerFixture>
{
	private readonly DynamoDbInboxStoreContainerFixture _fixture;
	private string? _tableName;

	// The store under test gets its OWN client so the fault can cut this store's ROUTE to DynamoDB
	// without touching the table or its rows. The fixture's shared client stays healthy, which is what
	// CreateVerificationStoreAsync reads the surviving data back through.
	private AmazonDynamoDBClient? _storeClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The DynamoDb container fixture.</param>
	public DynamoDbInboxStoreConformanceShould(DynamoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available for real-infra conformance (never skipped): " +
			$"{_fixture.InitializationError}");

		// Unique table per test for isolation. The store auto-creates it via InitializeAsync below.
		_tableName = $"{_fixture.TableName}_{Guid.NewGuid():N}";

		var options = Options.Create(new DynamoDbInboxOptions
		{
			TableName = _tableName,

			// Exercise the real consumer-supplied-client auto-create path.
			CreateTableIfNotExists = true,

			// Keep TTL auto-reap off so it never races the explicit-cleanup conformance tests.
			DefaultTtlSeconds = 0,
			Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
		});

		// Injected-client ctor preserves the supplied client; InitializeAsync runs CreateTableIfNotExists,
		// so the store creates its own table on the real consumer path (no fixture pre-create).
		_storeClient?.Dispose();
		_storeClient = new AmazonDynamoDBClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonDynamoDBConfig { ServiceURL = _fixture.ServiceUrl });

		var store = new DynamoDbInboxStore(_storeClient, options, NullLogger<DynamoDbInboxStore>.Instance, SingleTenantTestContext.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_tableName is not null)
		{
			await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);
			_tableName = null;
		}
	}

	// 47ruyr (2mek4x follow-up) asked for a real provider-side rejection rather than a mocked client, and
	// this arm used DeleteTableAsync to get one. That destroyed the evidence the SAFETY half exists to
	// inspect: with the table gone, the repair recreated it empty, so "nothing was persisted" could not
	// fail. DynamoDB has no rename and the emulator exposes no write-permission revoke, so the fault moves
	// to the ROUTING layer instead -- this store's own client is disposed, its writes can no longer reach
	// DynamoDB, and the table and every row in it survive untouched. Weaker than a provider-side rejection
	// and deliberately so: a fault that erases the evidence cannot prove the property.
	private DynamoDbInboxOptions VerificationOptions => new()
	{
		TableName = _tableName ?? throw new InvalidOperationException(
			"DynamoDbInboxStoreConformanceShould.CreateStoreAsync must run first."),
		CreateTableIfNotExists = true,
		DefaultTtlSeconds = 0,
		Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
	};

	/// <inheritdoc/>
	protected override Task InjectPersistenceFaultAsync()
	{
		_ = _storeClient ?? throw new InvalidOperationException(
			"DynamoDbInboxStoreConformanceShould.CreateStoreAsync must run before InjectPersistenceFaultAsync.");

		// Cuts this store's route to DynamoDB. Every subsequent PutItem fails at the client; the table,
		// its rows, and the fixture's shared client are all untouched.
		_storeClient.Dispose();
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		// The table and its rows were never altered -- there is no backing store to repair, which is the
		// whole point of moving the fault off the data. What must be restored is this store's ROUTE, and a
		// disposed client cannot be revived, so the store is rebuilt against a fresh one.
		//
		// Deliberately NOT via CreateStoreAsync: that randomises _tableName per call, so it would hand back
		// a store pointed at a brand-new empty table -- reintroducing exactly the defect this bead removes,
		// where the read-back queries somewhere the faulted write could never have reached. VerificationOptions
		// pins the EXISTING table, so the rebuilt store reads the same surviving rows.
		_storeClient?.Dispose();
		_storeClient = new AmazonDynamoDBClient(
			new BasicAWSCredentials("test", "test"),
			new AmazonDynamoDBConfig { ServiceURL = _fixture.ServiceUrl });

		var rebuilt = new DynamoDbInboxStore(
			_storeClient, Options.Create(VerificationOptions), NullLogger<DynamoDbInboxStore>.Instance,
			SingleTenantTestContext.Instance);
		await rebuilt.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
		Store = rebuilt;
	}

	// CreateStoreAsync randomises _tableName per call for test isolation, so a naive second CreateStoreAsync
	// call for the fresh-instance read-back would point at a namespace of its own (same trap as Redis's
	// KeyPrefix) -- reuse the PRIMARY store's own table instead. Lazily self-initializes on first use, so no
	// explicit InitializeAsync call is needed here (unlike RemovePersistenceFaultAsync, which needs the table
	// to exist for the ORIGINAL store's own next write and so forces it eagerly).
	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateVerificationStoreAsync()
	{
		IInboxStore store = new DynamoDbInboxStore(
			_fixture.Client, Options.Create(VerificationOptions), NullLogger<DynamoDbInboxStore>.Instance,
			SingleTenantTestContext.Instance);
		return Task.FromResult(store);
	}
}
