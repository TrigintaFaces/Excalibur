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
			await store.SaveAsync(NewSaga(completed: false, tenantId: "tenant-a"), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: "tenant-a"), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: "tenant-b"), ct).ConfigureAwait(false);
			await store.SaveAsync(NewSaga(completed: false, tenantId: null), ct).ConfigureAwait(false);

			var tenantA = await admin.QuerySagasAsync(new SagaQueryFilter { TenantId = "tenant-a" }, ct).ConfigureAwait(false);

			tenantA.Count.ShouldBe(2);
			tenantA.ShouldAllBe(s => s.TenantId == "tenant-a");
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
			for (var i = 0; i < 5; i++)
			{
				await store.SaveAsync(NewSaga(completed: false, tenantId: "page"), ct).ConfigureAwait(false);
			}

			var firstPage = await admin.QuerySagasAsync(
				new SagaQueryFilter { TenantId = "page", Skip = 0, MaxResults = 2 }, ct).ConfigureAwait(false);
			var lastPage = await admin.QuerySagasAsync(
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
			var saga = NewSaga(completed: false, tenantId: "tenant-x");
			await store.SaveAsync(saga, ct).ConfigureAwait(false);

			var found = await admin.GetSummaryAsync(saga.SagaId, ct).ConfigureAwait(false);
			var missing = await admin.GetSummaryAsync(Guid.NewGuid(), ct).ConfigureAwait(false);

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
	/// <inheritdoc/>
	protected override Task<(ISagaStore Store, ISagaStoreAdmin Admin)> CreateAsync()
	{
		// A fresh provider per test → per-test isolation without a cleanup step.
		var provider = new ServiceCollection().AddInMemorySagaStore().BuildServiceProvider();
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
			new DispatchJsonSerializer());
		return (store, (ISagaStoreAdmin)store);
	}

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

		var store = new PostgresSagaStore(options, NullLogger<PostgresSagaStore>.Instance, new DispatchJsonSerializer());
		return (store, (ISagaStoreAdmin)store);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();
}
