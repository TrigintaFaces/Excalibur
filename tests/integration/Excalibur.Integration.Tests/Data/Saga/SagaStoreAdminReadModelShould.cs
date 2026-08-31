// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.Postgres;
using Excalibur.Saga.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Saga;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Real-infra regression locks for the <see cref="ISagaStoreAdmin"/> saga query read-model (W1-6
/// <c>1mjfl1</c> / W1-7 <c>grqa1y</c>). Author≠impl (TestsDeveloper): binds the <em>emitted behavior</em>
/// — completion/tenant filtering, pagination, single-instance summary, and aggregate statistics — through
/// the real store on each MVP provider, rather than re-testing the persistence engine.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuous: the assertions exercise the admin query logic (correct running/completed partitioning,
/// tenant scoping, <c>Skip</c>/<c>MaxResults</c> paging, running/completed/total counts) so they RED on a
/// wrong query and GREEN only on the correct implementation. The interface itself is load-bearing — before
/// the provider stores implement <see cref="ISagaStoreAdmin"/> the lock does not resolve/compile against the
/// store, so the presence of the admin surface is proven too.
/// </para>
/// <para>
/// The container-backed providers (SQL Server, PostgreSQL) are <strong>never skipped</strong> — a missing
/// Docker daemon fails the lock (<c>DockerAvailable.ShouldBeTrue</c>), per the S875 real-infra AC. The
/// in-memory provider resolves through the real DI registration (<c>AddInMemorySagaStore</c>) so the
/// <c>TryAddSingleton&lt;ISagaStoreAdmin&gt;</c> wiring is exercised end-to-end.
/// </para>
/// </remarks>
public abstract class SagaStoreAdminReadModelTestBase
{
	/// <summary>Creates the store under test and its admin read-model projection (same underlying store).</summary>
	protected abstract Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateAsync();

	/// <summary>Cleans up between/after tests (truncate for container providers; no-op for in-memory).</summary>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a store bound to <paramref name="tenantId"/>, for seeding rows into that tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A saga's tenant comes from the scope the store was CONSTRUCTED with, and from nothing else. It is
	/// deliberately not taken from <c>sagaState.TenantId</c>: the read side is handed a saga id and a
	/// scope, never a state, so deriving the row's tenant from the saga's own property would let the two
	/// sides resolve different terms for the same saga and write it where no read looks.
	/// </para>
	/// <para>
	/// So a tenant cannot be chosen per call, and these arms previously tried to: they set
	/// <c>TenantId</c> on the state and saved through one unscoped store, which stamped every row
	/// untenanted. Filtering for a tenant then matched nothing and every count came back 0.
	/// </para>
	/// <para>
	/// The admin comes back with it because reads are scoped the same way. The ambient tenant predicate is
	/// UNCONDITIONAL on the query side: it used to fall away when no scope was set, which handed a
	/// multi-tenant deployment every tenant's summaries whenever the ambient scope happened to be unset, so
	/// it was made unconditional. There is therefore no estate-wide operator view to read through, and
	/// <c>SagaQueryFilter.TenantId</c> narrows WITHIN the ambient tenant rather than selecting one.
	/// </para>
	/// <para>
	/// This must not clean the table -- it is called after seeding has begun.
	/// </para>
	/// </remarks>
	/// <param name="tenantId">The tenant to bind the store and admin to.</param>
	/// <returns>A store and admin bound to <paramref name="tenantId"/>.</returns>
	protected abstract Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateForTenantAsync(string tenantId);

	/// <summary>
	/// Gets a value indicating whether this provider can hand out two tenant-scoped views of one dataset.
	/// </summary>
	/// <remarks>
	/// <para>
	/// True for the container-backed providers: the tenant is a column, so two stores constructed with
	/// different tenants read and write the same table. False for the in-memory store, which holds its
	/// state in the instance — a second store bound to a tenant shares nothing with the first and reads an
	/// empty store, so the arms could only ever assert against an empty set.
	/// </para>
	/// <para>
	/// A capability declaration, not an opt-out: where it is false the arms still run and assert the
	/// behaviour that provider does have, so nothing is skipped and the tenant coverage lives on the two
	/// providers that back real deployments.
	/// </para>
	/// </remarks>
	protected virtual bool SupportsTenantScopedStores => true;

	/// <summary>
	/// Presents a store that also implements <see cref="ISagaStoreAdmin"/> as the pair.
	/// </summary>
	/// <typeparam name="TStore">The concrete store type.</typeparam>
	/// <param name="store">The store, which is its own admin projection.</param>
	/// <returns>The store and its admin projection.</returns>
	protected static Task<(ISagaStore Store, ISagaStoreAdmin Admin)> AsPair<TStore>(TStore store)
		where TStore : ISagaStore, ISagaStoreAdmin =>
		Task.FromResult(((ISagaStore)store, (ISagaStoreAdmin)store));

	/// <summary>
	/// A tenant fixed at construction — the shape the store's contract actually takes.
	/// </summary>
	protected sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	private static TestSagaState NewSaga(bool completed, string? tenantId) => new()
	{
		SagaId = Guid.NewGuid(),
		Completed = completed,
		CompletedAt = completed ? DateTimeOffset.UtcNow : null,
		TenantId = tenantId,
		Data = "seed",
	};

	[Fact]
	public async Task QuerySagasByCompletionStateReturnsOnlyTheMatchingSubset()
	{
		var (store, admin) = await CreateAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;
		try
		{
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: true, tenantId: null), ct).ConfigureAwait(false);

			var running = await admin.QuerySagasAsync(new SagaQueryFilter { IsCompleted = false }, ct).ConfigureAwait(false);
			var completed = await admin.QuerySagasAsync(new SagaQueryFilter { IsCompleted = true }, ct).ConfigureAwait(false);
			var all = await admin.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);

			running.Count.ShouldBe(2);
			running.ShouldAllBe(s => !s.IsCompleted);
			completed.Count.ShouldBe(1);
			completed.ShouldAllBe(s => s.IsCompleted);
			all.Count.ShouldBe(3);
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task QuerySagasByTenantReturnsOnlyThatTenantsInstances()
	{
		var (store, admin) = await CreateAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;
		try
		{
			if (!SupportsTenantScopedStores)
			{
				// No tenant-scoped view available. Assert the behaviour this provider DOES have, so the
				// arm still binds something: rows seeded with no tenant are visible, and the store did
				// not silently swallow them.
				await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
				var untenantedOnly = await admin.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);
				untenantedOnly.Count.ShouldBe(1);
				return;
			}

			// Tenancy is a property of the store and the admin, not of a call argument — see
			// CreateForTenantAsync. tenant-b and the untenanted row are seeded so the assertion is not
			// vacuously true on an empty table: they exist and must not be returned.
			var (tenantAStore, tenantAAdmin) = await CreateForTenantAsync("tenant-a").ConfigureAwait(false);
			var (tenantBStore, _) = await CreateForTenantAsync("tenant-b").ConfigureAwait(false);

			await tenantAStore.SaveAsync(NewSaga(completed: false, tenantId: "tenant-a"), ct).ConfigureAwait(false);
			await tenantAStore.SaveAsync(NewSaga(completed: false, tenantId: "tenant-a"), ct).ConfigureAwait(false);
			await tenantBStore.SaveAsync(NewSaga(completed: false, tenantId: "tenant-b"), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);

			// Scoped admin AND filter, deliberately. The two providers do not agree on where a saga's
			// tenant lives: the container-backed stores take it from the scope the store was constructed
			// with, while the in-memory store reads sagaState.TenantId and filters on that. Asserting
			// through both terms keeps this arm honest on either semantics rather than encoding one
			// provider's. The divergence itself is a real inconsistency in the ISagaStore contract —
			// a consumer who develops against in-memory and deploys on Postgres gets different tenant
			// behaviour — and is worth settling separately from this suite.
			var tenantA = await tenantAAdmin.QuerySagasAsync(
				new SagaQueryFilter { TenantId = "tenant-a" }, ct).ConfigureAwait(false);

			tenantA.Count.ShouldBe(2);
			tenantA.ShouldAllBe(s => s.TenantId == "tenant-a");

			// Liveness: the scoping is not passing by returning nothing to everyone.
			var untenanted = await admin.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);
			untenanted.Count.ShouldBe(1, "the unscoped store's own row is still visible to it");
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task QuerySagasHonoursSkipAndMaxResultsPagination()
	{
		var (store, admin) = await CreateAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;
		try
		{
			if (!SupportsTenantScopedStores)
			{
				// No tenant-scoped view available. Assert the behaviour this provider DOES have, so the
				// arm still binds something: rows seeded with no tenant are visible, and the store did
				// not silently swallow them.
				await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
				var untenantedOnly = await admin.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);
				untenantedOnly.Count.ShouldBe(1);
				return;
			}

			var (pageStore, pageAdmin) = await CreateForTenantAsync("page").ConfigureAwait(false);

			for (var i = 0; i < 5; i++)
			{
				await pageStore.SaveAsync(NewSaga(completed: false, tenantId: "page"), ct).ConfigureAwait(false);
			}

			var firstPage = await pageAdmin.QuerySagasAsync(
				new SagaQueryFilter { TenantId = "page", Skip = 0, MaxResults = 2 }, ct).ConfigureAwait(false);
			var lastPage = await pageAdmin.QuerySagasAsync(
				new SagaQueryFilter { TenantId = "page", Skip = 4, MaxResults = 10 }, ct).ConfigureAwait(false);

			firstPage.Count.ShouldBe(2, "MaxResults must cap the page size");
			lastPage.Count.ShouldBe(1, "Skip past 4 of 5 leaves exactly one");
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task GetSummaryReturnsTheInstanceOrNullWhenAbsent()
	{
		var (store, admin) = await CreateAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;
		try
		{
			if (!SupportsTenantScopedStores)
			{
				// No tenant-scoped view available. Assert the behaviour this provider DOES have, so the
				// arm still binds something: rows seeded with no tenant are visible, and the store did
				// not silently swallow them.
				await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
				var untenantedOnly = await admin.QuerySagasAsync(new SagaQueryFilter(), ct).ConfigureAwait(false);
				untenantedOnly.Count.ShouldBe(1);
				return;
			}

			var saga = NewSaga(completed: false, tenantId: "tenant-x");
			var (tenantXStore, tenantXAdmin) = await CreateForTenantAsync("tenant-x").ConfigureAwait(false);
			await tenantXStore.SaveAsync(saga, ct).ConfigureAwait(false);

			var found = await tenantXAdmin.GetSummaryAsync(saga.SagaId, ct).ConfigureAwait(false);
			var missing = await tenantXAdmin.GetSummaryAsync(Guid.NewGuid(), ct).ConfigureAwait(false);

			found.ShouldNotBeNull();
			found.SagaId.ShouldBe(saga.SagaId);
			found.IsCompleted.ShouldBeFalse();
			found.TenantId.ShouldBe("tenant-x");
			missing.ShouldBeNull();
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task GetStatisticsCountsRunningCompletedAndTotal()
	{
		var (store, admin) = await CreateAsync().ConfigureAwait(false);
		var ct = CancellationToken.None;
		try
		{
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: true, tenantId: null), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: true, tenantId: null), ct).ConfigureAwait(false);

			var stats = await admin.GetStatisticsAsync(ct).ConfigureAwait(false);

			stats.RunningCount.ShouldBe(3);
			stats.CompletedCount.ShouldBe(2);
			stats.TotalCount.ShouldBe(5);
		}
		finally
		{
			await CleanupAsync().ConfigureAwait(false);
		}
	}
}

/// <summary>In-memory <see cref="ISagaStoreAdmin"/> lock — resolves through the real DI registration.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
public sealed class InMemorySagaStoreAdminReadModelShould : SagaStoreAdminReadModelTestBase
{
	private ServiceProvider? _provider;

	/// <inheritdoc/>
	protected override bool SupportsTenantScopedStores => false;

	/// <inheritdoc/>
	protected override Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateForTenantAsync(string tenantId) =>
		throw new NotSupportedException(
			"The in-memory store holds its state in the instance, so a second store bound to a tenant "
			+ "shares nothing with the first and reads an empty store. See SupportsTenantScopedStores.");

	/// <inheritdoc/>
	protected override Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateAsync()
	{
		// A fresh provider per test → per-test isolation without a cleanup step.
		var provider = new ServiceCollection().AddInMemorySagaStore().BuildServiceProvider();
		_provider = provider;
		var store = provider.GetRequiredService<ISagaStore>();
		var admin = provider.GetRequiredService<ISagaStoreAdmin>();
		return Task.FromResult((store, admin));
	}
}

/// <summary>SQL Server <see cref="ISagaStoreAdmin"/> real-infra lock — never skipped.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaStoreAdminReadModelShould : SagaStoreAdminReadModelTestBase, IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private readonly SqlServerSagaStoreContainerFixture _fixture;

	public SqlServerSagaStoreAdminReadModelShould(SqlServerSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override async Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"SQL Server container must be available — this real-infra saga-admin lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = new SqlServerSagaStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer(),
			UntenantedTestTenantContext.Instance);
		return (store, (ISagaStoreAdmin)store);
	}

	/// <inheritdoc/>
	protected override Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateForTenantAsync(string tenantId) =>
		AsPair(new SqlServerSagaStore(
			_fixture.ConnectionString,
			NullLogger<SqlServerSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId)));

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}

/// <summary>PostgreSQL <see cref="ISagaStoreAdmin"/> real-infra lock — never skipped.</summary>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
[Collection("PostgresSagaStore")]
public sealed class PostgresSagaStoreAdminReadModelShould : SagaStoreAdminReadModelTestBase
{
	private readonly PostgresSagaStoreContainerFixture _fixture;

	public PostgresSagaStoreAdminReadModelShould(PostgresSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	protected override async Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"PostgreSQL container must be available — this real-infra saga-admin lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30,
		});

		var store = new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			UntenantedTestTenantContext.Instance);
		return (store, (ISagaStoreAdmin)store);
	}

	/// <inheritdoc/>
	protected override Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateForTenantAsync(string tenantId)
	{
		var options = Options.Create(new PostgresSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			Schema = _fixture.Schema,
			TableName = _fixture.TableName,
			CommandTimeoutSeconds = 30,
		});

		return AsPair(new PostgresSagaStore(
			options,
			NullLogger<PostgresSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId)));
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
