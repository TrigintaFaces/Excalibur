// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds the refusal that stands between an unmigrated MongoDB saga collection and a silently restarted
/// saga: a document written under the pre-tenant identifier is unaddressable now, so a load of the saga it
/// holds reports NO SAGA IN FLIGHT - which the caller reads as a saga that has not begun, so it starts the
/// saga over and re-fires every compensating action and external call that has already happened. On the
/// create path the same silence lets a second, duplicate saga be inserted beside the original.
/// </summary>
/// <remarks>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed document and requires
/// an absent saga to load as <see langword="null"/> and a new saga to then be creatable. That reaches the
/// probe rather than bypassing it - an absent saga is exactly what triggers it - so the arm proves the
/// probe comes back clean, not merely that it never ran.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbSagaStoreLegacyKeyRefusalShould : IClassFixture<MongoDbContainerFixture>
{
	private const string DatabaseName = "excalibur_saga_legacy_key";
	private const string TenantA = "tenant-A";

	private readonly MongoDbContainerFixture _fixture;

	// One collection per test instance. xUnit builds a fresh instance per arm, so neither arm can observe
	// what the other seeded.
	private readonly string _collection = "saga_legacy_key_" + Guid.NewGuid().ToString("N");

	public MongoDbSagaStoreLegacyKeyRefusalShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY: a collection still holding a document written without a tenant segment is refused, by name,
	/// before it can be read back as "no saga in flight".
	/// </summary>
	[Fact]
	public async Task Refuse_a_collection_holding_a_document_written_without_a_tenant_segment()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB must be available - this arm exists to prove a real collection is refused, so it is "
			+ "never skipped");

		// The shape an earlier release wrote on this provider: the saga identifier alone as the _id.
		var legacySagaId = Guid.NewGuid();
		var legacyDocumentId = legacySagaId.ToString();
		await SeedDocumentAsync(legacyDocumentId, legacySagaId).ConfigureAwait(false);

		var loadRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.LoadAsync<TestSagaState>(legacySagaId, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		loadRefusal.Message.ShouldContain(
			_collection,
			Case.Sensitive,
			"the refusal must name the collection a consumer has to re-key, or it cannot be acted on");

		loadRefusal.Message.ShouldContain(
			legacyDocumentId,
			Case.Sensitive,
			"naming the offending identifier is what lets a consumer confirm which documents are affected");

		// The create path is guarded separately, and it is the one that produces a DUPLICATE saga rather
		// than a restarted one: the upsert addresses the new identifier, so the legacy document does not
		// collide with it and nothing refuses the second insert.
		var createRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.SaveAsync(
					new TestSagaState { SagaId = Guid.NewGuid(), TenantId = TenantA },
					CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		createRefusal.Message.ShouldContain(
			_collection,
			Case.Sensitive,
			"a create must refuse for the same reason a load does, and name the same collection");

		// The refusal is a refusal, not a repair: the document is still exactly where it was, and the
		// refused create wrote nothing beside it.
		(await CountDocumentsAsync().ConfigureAwait(false)).ShouldBe(
			1L,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the "
			+ "data - and the refused create must not have inserted its own document");
	}

	/// <summary>
	/// LIVENESS: a collection whose documents all carry a tenant segment is served normally. Without this
	/// arm a probe that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_collection_whose_documents_all_carry_a_tenant_segment()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB must be available - a correctly-keyed collection must remain fully usable, so this arm "
			+ "is never skipped");

		// Written through the store, so the seeded document carries exactly the identifier this release
		// composes rather than one the test invented.
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

		_ = reloaded.ShouldNotBeNull("a correctly-keyed collection must remain fully writable");
	}

	private MongoDbSagaStore CreateStore(string? tenantId) =>
		new(
			new MongoClient(_fixture.ConnectionString),
			Options.Create(new MongoDbSagaOptions
			{
				ConnectionString = _fixture.ConnectionString,
				DatabaseName = DatabaseName,
				CollectionName = _collection,
			}),
			NullLogger<MongoDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	private IMongoCollection<BsonDocument> Collection() =>
		new MongoClient(_fixture.ConnectionString)
			.GetDatabase(DatabaseName)
			.GetCollection<BsonDocument>(_collection);

	// Seeded through the raw driver rather than through the store, because the store can no longer write the
	// shape under test - that is the whole point of the change this locks.
	private async Task SeedDocumentAsync(string documentId, Guid sagaId) =>
		await Collection().InsertOneAsync(new BsonDocument
		{
			["_id"] = documentId,
			["sagaId"] = sagaId.ToString(),
			["sagaType"] = nameof(TestSagaState),
			["stateJson"] = "{}",
			["isCompleted"] = false,
			["version"] = 1L,
			["createdUtc"] = DateTime.UtcNow,
			["updatedUtc"] = DateTime.UtcNow,
		}).ConfigureAwait(false);

	private async Task<long> CountDocumentsAsync() =>
		await Collection().CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
