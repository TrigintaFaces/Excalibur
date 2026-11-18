// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Saga;

[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Collection("SqlServer SagaStore Integration Tests")]
public sealed class SqlServerSagaExactTenantShould(SqlServerSagaStoreContainerFixture fixture)
	: IClassFixture<SqlServerSagaStoreContainerFixture>
{
	private const string Owner = "tenantA";

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Refuse_alias_matching_version_overwrite(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var foreign = Create(alias);
		var state = new ExactTenantState { TenantId = Owner, Payload = "victim" };
		await owner.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		var persisted = await owner.LoadAsync<ExactTenantState>(state.SagaId, CancellationToken.None).ConfigureAwait(false);
		persisted.ShouldNotBeNull();
		persisted.Version.ShouldBeGreaterThan(0);
		await Should.ThrowAsync<ConcurrencyException>(() => foreign.SaveAsync(
			new ExactTenantState { SagaId = state.SagaId, TenantId = alias, Version = persisted.Version, Payload = "intruder" },
			CancellationToken.None)).ConfigureAwait(false);
		var retained = await owner.LoadAsync<ExactTenantState>(state.SagaId, CancellationToken.None).ConfigureAwait(false);
		retained.ShouldNotBeNull();
		retained.Payload.ShouldBe("victim");
		retained.Version.ShouldBe(persisted.Version);
		retained.Payload = "owner-update";
		await owner.SaveAsync(retained, CancellationToken.None).ConfigureAwait(false);
		(await owner.LoadAsync<ExactTenantState>(state.SagaId, CancellationToken.None).ConfigureAwait(false))!
			.Payload.ShouldBe("owner-update");
	}

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Scope_query_summary_and_statistics_by_exact_tenant(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var foreign = Create(alias);
		var a = new ExactTenantState { TenantId = Owner };
		var b = new ExactTenantState { TenantId = alias, Completed = true, CompletedAt = DateTimeOffset.UtcNow.AddDays(-2) };
		await owner.SaveAsync(a, CancellationToken.None).ConfigureAwait(false);
		await foreign.SaveAsync(b, CancellationToken.None).ConfigureAwait(false);
		var ownRows = await owner.QuerySagasAsync(new SagaQueryFilter(), CancellationToken.None).ConfigureAwait(false);
		ownRows.Select(row => row.SagaId).ShouldBe(new[] { a.SagaId });
		(await owner.QuerySagasAsync(new SagaQueryFilter { TenantId = alias }, CancellationToken.None).ConfigureAwait(false)).ShouldBeEmpty();
		(await foreign.QuerySagasAsync(new SagaQueryFilter(), CancellationToken.None).ConfigureAwait(false))
			.Select(row => row.SagaId).ShouldBe(new[] { b.SagaId });
		(await foreign.GetSummaryAsync(a.SagaId, CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
		(await owner.GetSummaryAsync(a.SagaId, CancellationToken.None).ConfigureAwait(false))!.SagaId.ShouldBe(a.SagaId);
		var stats = await owner.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.TotalCount.ShouldBe(1);
		stats.RunningCount.ShouldBe(1);
		stats.CompletedCount.ShouldBe(0);
		var foreignStats = await foreign.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		foreignStats.TotalCount.ShouldBe(1);
		foreignStats.CompletedCount.ShouldBe(1);
		var global = await owner.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		global.TotalCount.ShouldBe(2);
		global.RunningCount.ShouldBe(1);
		global.CompletedCount.ShouldBe(1);
	}

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Purge_only_exact_scope_and_keep_explicit_global_purge(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var foreign = Create(alias);
		var threshold = DateTimeOffset.UtcNow.AddDays(-1);
		var a = new ExactTenantState { TenantId = Owner, Completed = true, CompletedAt = threshold.AddDays(-1) };
		var b = new ExactTenantState { TenantId = alias, Completed = true, CompletedAt = threshold.AddDays(-1) };
		await owner.SaveAsync(a, CancellationToken.None).ConfigureAwait(false);
		await foreign.SaveAsync(b, CancellationToken.None).ConfigureAwait(false);
		(await owner.PurgeCompletedBeforeAsync(threshold, CancellationToken.None).ConfigureAwait(false)).ShouldBe(1);
		(await owner.LoadAsync<ExactTenantState>(a.SagaId, CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
		(await foreign.LoadAsync<ExactTenantState>(b.SagaId, CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
		(await owner.PurgeAllTenantsCompletedBeforeAsync(threshold, CancellationToken.None).ConfigureAwait(false)).ShouldBe(1);
		(await foreign.LoadAsync<ExactTenantState>(b.SagaId, CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
	}

	[Theory]
	[InlineData("tenantA ")]
	[InlineData("TENANTA")]
	public async Task Refuse_alias_load(string alias)
	{
		await InitializeAsync().ConfigureAwait(false);
		var owner = Create(Owner);
		var state = new ExactTenantState { TenantId = Owner, Payload = "victim" };
		await owner.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		(await Create(alias).LoadAsync<ExactTenantState>(state.SagaId, CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
		(await owner.LoadAsync<ExactTenantState>(state.SagaId, CancellationToken.None).ConfigureAwait(false))!.Payload.ShouldBe("victim");
	}
	private async Task InitializeAsync()
	{
		fixture.DockerAvailable.ShouldBeTrue("Real SQL isolation tests must not skip unavailable infrastructure.");
		await fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await SqlServerExactTenantEvidence.RequireSagaCollationAsync(fixture.ConnectionString).ConfigureAwait(false);
		await fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	private SqlServerSagaStore Create(string tenant) => new(fixture.ConnectionString,
		Options.Create(new SqlServerSagaStoreOptions { SchemaName = fixture.SchemaName, TableName = fixture.TableName }),
		NullLogger<SqlServerSagaStore>.Instance, new DispatchJsonSerializer(), new ExactSqlTenantContext(tenant));

	private sealed class ExactTenantState : SagaState
	{
		public string Payload { get; set; } = string.Empty;
	}
}
