// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MongoDbInboxStore"/> using the Inbox
/// Conformance Test Kit against a live MongoDB container.
/// </summary>
/// <remarks>
/// These tests verify that the MongoDB implementation correctly implements the
/// <see cref="IInboxStore"/> contract — including idempotent first-writer-wins processing, the
/// set-exactly retry-count overload, and atomic status transitions — using TestContainers. They are
/// never skipped: when Docker is unavailable the fixture fails fast, so a missing container surfaces as
/// a failure rather than a silent pass. The store is constructed via its options-only constructor, which
/// builds the provider's DEFAULT <c>MongoClient</c> (and therefore the default serializer) from the
/// connection string — the surface a normal consumer uses. The store self-initializes its collection and
/// indexes on first use.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<MongoDbInboxStoreContainerFixture>
{
	private readonly MongoDbInboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbInboxStoreConformanceShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});

		// Options-only constructor: the store builds the provider's DEFAULT MongoClient (default
		// serializer) from the connection string — the surface most consumers use. The store
		// self-initializes its collection and indexes on first use.
		return Task.FromResult<IInboxStore>(
			new MongoDbInboxStore(options, NullLogger<MongoDbInboxStore>.Instance, SingleTenantTestContext.Instance));
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	// 2mek4x: a real, provider-side persistence rejection -- never a mocked client. A document validator
	// requiring a field the store never writes makes the server reject every insert/update against the
	// collection with a genuine WriteError, the same shape of failure a consumer would see from a real
	// misconfiguration (e.g. a validator or a schema migration left the collection in a state the store's
	// writes cannot satisfy).

	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		using var faultClient = new MongoClient(_fixture.ConnectionString);
		var database = faultClient.GetDatabase(_fixture.DatabaseName);
		var collectionName = new MongoDbInboxOptions().CollectionName;

		try
		{
			// The store creates the collection lazily on first use; this fault-injection test may run
			// before any other operation has touched it, so ensure it exists before collMod-ing it.
			await database.CreateCollectionAsync(collectionName).ConfigureAwait(false);
		}
		catch (MongoCommandException ex) when (string.Equals(ex.CodeName, "NamespaceExists", StringComparison.Ordinal))
		{
			// Already created by an earlier test's use of the shared fixture's Store -- nothing to do.
		}

		var command = new BsonDocument
		{
			{
				"collMod", collectionName
			},
			{
				"validator", new BsonDocument("$jsonSchema", new BsonDocument
				{
					{ "bsonType", "object" },
					{ "required", new BsonArray { "__2mek4x_deliberately_impossible_field__" } },
				})
			},
			{ "validationLevel", "strict" },
			{ "validationAction", "error" },
		};

		_ = await database.RunCommandAsync<BsonDocument>(command).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		using var faultClient = new MongoClient(_fixture.ConnectionString);
		var database = faultClient.GetDatabase(_fixture.DatabaseName);
		var collectionName = new MongoDbInboxOptions().CollectionName;

		var command = new BsonDocument
		{
			{ "collMod", collectionName },
			{ "validator", new BsonDocument() },
			{ "validationLevel", "off" },
		};

		_ = await database.RunCommandAsync<BsonDocument>(command).ConfigureAwait(false);
	}
}
