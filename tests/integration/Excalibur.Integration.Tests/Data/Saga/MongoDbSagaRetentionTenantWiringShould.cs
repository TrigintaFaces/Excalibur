// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.MongoDB;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Bson;
using MongoDB.Driver;

using Tests.Shared.Conformance.Saga;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// NON-SKIPPED real-MongoDB lock on the MongoDB saga store's <b>retention sweep</b>, resolved through the
/// production registration seam rather than constructed by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this resolves through DI instead of calling the constructor.</b> The store already accepts an
/// ambient tenant context and already composes it into every filter, so a test that hands the store a
/// context directly proves only that the store works when it is given one. The defect this lock binds is
/// that the registration never gave it one: the factory built the store from client, options, logger and
/// serializer alone, leaving the context at its default. A hand-constructed store cannot see that, which
/// is why the existing hand-constructed tenant-isolation arms are all green against a store whose
/// retention sweep deletes nothing in a tenanted host. The seam under test is the registration.
/// </para>
/// <para>
/// <b>There are two registration shapes and only one of them was broken, so both are exercised.</b> A host
/// that supplies only a connection string gets a container-activated store, and the container resolves the
/// optional tenant-context parameter itself — that shape was never affected. A host that supplies its own
/// client or client factory gets an explicit factory instead, and that factory listed four arguments and
/// stopped, so the context defaulted away. Running the same pair of assertions over both shapes is what
/// distinguishes the defect from the mechanism that hid it: without the client-instance arm this lock is
/// green before and after the fix and proves nothing.
/// </para>
/// <para>
/// <b>Why the sweep silently deletes nothing.</b> The saga scope resolves through
/// <c>TenantScope.FromContext</c>, whose absent case is <c>None</c>, and an unscoped saga filter matches a
/// NULL tenant term. The save path is not symmetric with it: when no ambient tenant resolves, the write
/// falls back to the tenant carried on the saga state itself. A host that stamps its saga states therefore
/// writes real tenant terms and then sweeps for NULL ones, so the delete matches no row, reports success,
/// and the collection grows without bound. Nothing surfaces: the operation's own return value is a count
/// of rows deleted, and zero is indistinguishable from nothing being due.
/// </para>
/// <para>
/// <b>Both arms are required.</b> The safety property — one tenant's sweep must not delete another's
/// sagas — is satisfied perfectly by a sweep that deletes nothing at all, which is exactly the defect.
/// Only the liveness arm can tell a working filter from an inert one, so the arms are asserted as a pair
/// against observed collection state read back over a separate client, never against the returned count
/// alone.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbSagaRetentionTenantWiringShould : IClassFixture<MongoDbContainerFixture>
{
	private const string OwningTenant = "tenant-retention-a";
	private const string OtherTenant = "tenant-retention-b";

	private readonly MongoDbContainerFixture _fixture;
	private readonly string _databaseName = $"saga_retention_wiring_{Guid.NewGuid():N}";
	private readonly MutableTenantContext _tenantContext = new();

	public MongoDbSagaRetentionTenantWiringShould(MongoDbContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// LIVENESS and SAFETY as a pair: the owning tenant's completed saga is actually deleted, and the
	/// other tenant's completed saga actually survives.
	/// </summary>
	/// <remarks>
	/// The two tenants' sagas are equally old and equally completed, so age and completion cannot explain
	/// the difference in outcome — only the tenant term can. Asserting them together is what distinguishes
	/// a correctly scoped sweep from an inert one: an inert sweep satisfies the survival assertion and
	/// fails the deletion assertion, and a sweep with no tenant term at all satisfies the deletion
	/// assertion and fails the survival one.
	/// </remarks>
	/// <param name="registerClientInstance">
	/// <see langword="false"/> registers by connection string (container-activated store, the shape that
	/// always worked); <see langword="true"/> registers a caller-supplied <see cref="IMongoClient"/>, which
	/// routes through the explicit factory — the shape whose sweep matched nothing.
	/// </param>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Delete_the_owning_tenants_completed_sagas_while_leaving_another_tenants_intact(
		bool registerClientInstance)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available — an unbounded saga table is a durability failure, so "
			+ "this real-Mongo retention lock is never skipped.");

		var store = BuildStoreThroughRegistration(registerClientInstance);

		var completedAt = DateTimeOffset.UtcNow.AddHours(-2);
		var cutoff = DateTimeOffset.UtcNow.AddHours(-1);

		_tenantContext.TenantId = OwningTenant;
		await SaveCompletedSagaAsync(store, OwningTenant, completedAt).ConfigureAwait(false);

		_tenantContext.TenantId = OtherTenant;
		await SaveCompletedSagaAsync(store, OtherTenant, completedAt).ConfigureAwait(false);

		// Control: both rows are on disk before the sweep, so a later zero is attributable to the sweep's
		// predicate rather than to a write that never landed or a container that never came up.
		(await CountDocumentsForTenantAsync(OwningTenant).ConfigureAwait(false)).ShouldBe(
			1,
			"the owning tenant's saga must be persisted before the sweep, otherwise the deletion "
			+ "assertion below would pass for the wrong reason");
		(await CountDocumentsForTenantAsync(OtherTenant).ConfigureAwait(false)).ShouldBe(
			1,
			"the other tenant's saga must be persisted before the sweep, otherwise the survival "
			+ "assertion below would pass for the wrong reason");

		_tenantContext.TenantId = OwningTenant;
		var deleted = await store.PurgeCompletedBeforeAsync(cutoff, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS — observed collection state, not the returned count. A store wired without the ambient
		// context sweeps for a NULL tenant term and leaves this document exactly where it is.
		(await CountDocumentsForTenantAsync(OwningTenant).ConfigureAwait(false)).ShouldBe(
			0,
			"the owning tenant's completed saga is past the cutoff and must actually be gone from the "
			+ "collection: a retention sweep that reports success while deleting nothing lets the saga "
			+ "table grow without bound, and the only symptom is a table that never shrinks");

		// SAFETY — the sweep is confined. Asserted second so it cannot be mistaken for the whole contract.
		(await CountDocumentsForTenantAsync(OtherTenant).ConfigureAwait(false)).ShouldBe(
			1,
			"another tenant's completed saga is the same age and equally eligible by every term except "
			+ "the tenant, so it must survive a sweep run under the owning tenant");

		deleted.ShouldBe(
			1,
			"the reported count must agree with the collection: a sweep that deletes a row while "
			+ "reporting a different number misleads the operator reading its logs");
	}

	/// <summary>
	/// Registers the store the way a host does, then resolves it — the seam this lock exists to bind.
	/// </summary>
	/// <remarks>
	/// The ambient context is registered BEFORE the provider registration so the provider's fail-closed
	/// single-tenant default (a <c>TryAdd</c>) yields to it, which is the composition order a multi-tenant
	/// host produces. Nothing here hands the store its tenant context: if the registration does not thread
	/// the resolved context into construction, the resolved store has none.
	/// </remarks>
	private ISagaStore BuildStoreThroughRegistration(bool registerClientInstance)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton<ITenantContext>(_tenantContext);
		services.AddSingleton(new DispatchJsonSerializer());

		_ = new TestSagaBuilder(services).UseMongoDB(mongo =>
		{
			_ = mongo.DatabaseName(_databaseName).CollectionName("sagas");

			// Supplying a client is a first-class documented option, and it is the option that selects the
			// explicit factory rather than container activation.
			_ = registerClientInstance
				? mongo.Client(new MongoClient(_fixture.ConnectionString))
				: mongo.ConnectionString(_fixture.ConnectionString);
		});

		var provider = services.BuildServiceProvider();
		return provider.GetRequiredKeyedService<ISagaStore>("mongodb");
	}

	private static Task SaveCompletedSagaAsync(ISagaStore store, string tenantId, DateTimeOffset completedAt) =>
		store.SaveAsync(
			new TestSagaState
			{
				SagaId = Guid.NewGuid(),
				TenantId = tenantId,
				Completed = true,
				CompletedAt = completedAt,
			},
			CancellationToken.None);

	/// <summary>
	/// Reads observed collection state over an independent client, so the assertion cannot inherit the
	/// store's own tenant filter — the very thing under test.
	/// </summary>
	private async Task<long> CountDocumentsForTenantAsync(string tenantId)
	{
		var collection = new MongoClient(_fixture.ConnectionString)
			.GetDatabase(_databaseName)
			.GetCollection<BsonDocument>("sagas");

		return await collection
			.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("tenantId", tenantId))
			.ConfigureAwait(false);
	}

	/// <summary>A minimal saga builder over a service collection; the interface carries only Services.</summary>
	private sealed class TestSagaBuilder(IServiceCollection services) : ISagaBuilder
	{
		public IServiceCollection Services { get; } = services;
	}

	/// <summary>An ambient context whose tenant the test moves between saves, as a resolver would.</summary>
	private sealed class MutableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
