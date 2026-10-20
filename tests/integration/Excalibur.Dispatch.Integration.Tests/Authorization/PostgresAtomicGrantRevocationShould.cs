// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Transactions;

using Dapper;

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>Exercises atomic grant authority against the shipped PostgreSQL schema.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresAtomicGrantRevocationShould(PostgresFixture fixture) : IAsyncLifetime
{
	private readonly string _user = $"atomic-{Guid.NewGuid():N}";

	/// <inheritdoc />
	public async ValueTask InitializeAsync() => await ShippedGrantSchema.ProvisionPostgresAsync(
		fixture.ConnectionString, TestContext.Current.CancellationToken);

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	[Fact]
	public async Task RevokeEveryTypeOnlyInTheExactTenantAndAuditEveryRemovedRow()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		await SaveAsync(store, "Tenant", "Role", "role", ct);
		await SaveAsync(store, "Tenant", "ActivityGroup", "group", ct);
		await SaveAsync(store, "Tenant", "Custom", "custom", ct);
		await SaveAsync(store, "tenant", "ActivityGroup", "other-tenant", ct);
		var before = await store.ReadUserRevisionAsync(_user, ct);

		var result = await store.RevokeAllAsync(Request(GrantRevocationScope.ForTenant("Tenant")), ct);

		result.RevokedCount.ShouldBe(3);
		result.Revision.ShouldBe(before + 1);
		var snapshot = await store.ReadUserSnapshotAsync(_user, true, ct);
		snapshot.Revision.ShouldBe(result.Revision);
		snapshot.Grants.Select(g => g.Qualifier).ShouldBe(["other-tenant"]);
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		var history = await connection.QueryAsync<(string TenantId, string RevokedBy)>(
			"SELECT tenant_id, revoked_by FROM authz.grant_history WHERE user_id = @UserId", new { UserId = _user });
		history.Count().ShouldBe(3);
		history.All(row => row.TenantId == "Tenant" && row.RevokedBy == "operator").ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnTheFirstOutcomeOnRetryAndPreserveGrantsAddedAfterCommit()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		await SaveAsync(store, "one", "ActivityGroup", "old", ct);
		await SaveAsync(store, "two", "Role", "old", ct);
		var request = Request(GrantRevocationScope.AllTenants);
		var first = await store.RevokeAllAsync(request, ct);
		await SaveAsync(store, "one", "Role", "new", ct);
		var afterAdd = await store.ReadUserRevisionAsync(_user, ct);

		(await store.RevokeAllAsync(request, ct)).ShouldBe(first);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(afterAdd);
		(await store.ReadUserSnapshotAsync(_user, true, ct)).Grants.Select(g => g.Qualifier).ShouldBe(["new"]);
		await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(
			new GrantRevocationRequest(request.OperationId, _user, GrantRevocationScope.ForTenant("one"), "operator"), ct));
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(afterAdd);
	}

	[Fact]
	public async Task RollBackTheWholeDeleteWhenTheAuditInsertFails()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		await SaveAsync(store, "one", "Role", "role", ct);
		await SaveAsync(store, "one", "ActivityGroup", "group", ct);
		var before = await store.ReadUserSnapshotAsync(_user, true, ct);
		// The history actor column is narrower than the ledger's text column. The delete CTE and audit
		// execute on the server; an audit failure must roll back rows, authority, and operation ledger.
		var failure = await Should.ThrowAsync<PostgresException>(() => store.RevokeAllAsync(
			new GrantRevocationRequest(Guid.NewGuid(), _user, GrantRevocationScope.AllTenants, new string('x', 129)), ct));
		failure.SqlState.ShouldBe("22001");
		var after = await store.ReadUserSnapshotAsync(_user, true, ct);
		after.Revision.ShouldBe(before.Revision);
		after.Grants.OrderBy(g => g.Qualifier).ShouldBe(before.Grants.OrderBy(g => g.Qualifier));
	}

	[Fact]
	public async Task AdvanceTheAuthorityForEveryActivityGroupWriter()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		var revision = await store.ReadUserRevisionAsync(_user, ct);
		await store.InsertActivityGroupGrantAsync(_user, "Name", "one", "ActivityGroup", "old", null, "operator", ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(++revision);
		await store.DeleteActivityGroupGrantsByUserIdAsync(_user, "ActivityGroup", ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(++revision);
		await store.ReplaceActivityGroupGrantsForUserAsync(_user, "ActivityGroup", Snapshot("new"), ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(++revision);
		await store.ReplaceActivityGroupGrantsAsync("ActivityGroup", Snapshot("estate"), ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(++revision);
		await store.DeleteAllActivityGroupGrantsAsync("ActivityGroup", ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(++revision);
	}

	[Fact]
	public async Task RefuseAmbientTransactionsBeforeMutation()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		await SaveAsync(store, "one", "Role", "preserved", ct);
		var before = await store.ReadUserRevisionAsync(_user, ct);
		using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
		{
			await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(Request(GrantRevocationScope.AllTenants), ct));
			await Should.ThrowAsync<InvalidOperationException>(() => SaveAsync(store, "one", "Role", "refused", ct));
		}
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(before);
		(await store.ReadUserSnapshotAsync(_user, true, ct)).Grants.Select(g => g.Qualifier).ShouldBe(["preserved"]);
	}

	[Fact]
	public async Task AdvanceSaveAndSingleDeleteAndRefuseCancellationWithoutMutation()
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		var initial = await store.ReadUserRevisionAsync(_user, ct);
		await SaveAsync(store, "one", "Role", "role", ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(initial + 1);
		await store.DeleteGrantAsync(_user, "one", "Role", "role", "operator", DateTimeOffset.UtcNow, ct);
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(initial + 2);
		await SaveAsync(store, "one", "ActivityGroup", "preserved", ct);
		var before = await store.ReadUserRevisionAsync(_user, ct);
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => store.RevokeAllAsync(
			Request(GrantRevocationScope.AllTenants), cancelled.Token));
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(before);
		(await store.ReadUserSnapshotAsync(_user, true, ct)).Grants.Select(g => g.Qualifier).ShouldBe(["preserved"]);
	}

	[Fact]
	public async Task SerializeConcurrentRetriesToOneAuditedCommit()
	{
		using var dbA = CreateDb();
		using var dbB = CreateDb();
		var a = new PostgresGrantStore(dbA);
		var b = new PostgresGrantStore(dbB);
		var ct = TestContext.Current.CancellationToken;
		await SaveAsync(a, "one", "Role", "role", ct);
		var request = Request(GrantRevocationScope.AllTenants);
		var results = await Task.WhenAll(a.RevokeAllAsync(request, ct), b.RevokeAllAsync(request, ct));
		results[0].ShouldBe(results[1]);
		results[0].RevokedCount.ShouldBe(1);
		(await a.ReadUserRevisionAsync(_user, ct)).ShouldBe(results[0].Revision);
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		(await connection.ExecuteScalarAsync<int>("SELECT count(*)::integer FROM authz.grant_history WHERE user_id = @UserId",
			new { UserId = _user })).ShouldBe(1);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("tenant-A")]
	[InlineData("__untenanted__")]
	public async Task PreserveTheExplicitOperationScopeOnReplay(string? tenant)
	{
		using var db = CreateDb();
		var store = new PostgresGrantStore(db);
		var ct = TestContext.Current.CancellationToken;
		var selectedTenant = tenant ?? "tenant-A";
		await SaveAsync(store, selectedTenant, "ActivityGroup", "before", ct);
		await SaveAsync(store, "tenant-B", "Activity", "other", ct);
		var request = Request(tenant is null ? GrantRevocationScope.AllTenants : GrantRevocationScope.ForTenant(tenant));
		var first = await store.RevokeAllAsync(request, ct);
		first.RevokedCount.ShouldBe(tenant is null ? 2 : 1);
		await SaveAsync(store, selectedTenant, "Activity", "after", ct);
		(await store.RevokeAllAsync(request, ct)).ShouldBe(first);
		var conflicting = new GrantRevocationRequest(request.OperationId, _user,
			tenant is null ? GrantRevocationScope.ForTenant(selectedTenant) : GrantRevocationScope.AllTenants, request.RevokedBy);
		await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(conflicting, ct));
		(await store.ReadUserRevisionAsync(_user, ct)).ShouldBe(first.Revision + 1);
		(await store.ReadUserSnapshotAsync(_user, true, ct)).Grants.Select(g => g.Qualifier).ShouldContain("after");
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		(await connection.ExecuteScalarAsync<int>(new CommandDefinition(
			"SELECT count(*)::integer FROM authz.grant_history WHERE user_id = @UserId",
			new { UserId = _user }, cancellationToken: ct))).ShouldBe(first.RevokedCount);
		(await connection.QuerySingleAsync<string?>(new CommandDefinition(
			"SELECT scope_tenant_id FROM authz.grant_revocation WHERE user_id = @UserId AND operation_id = @OperationId",
			new { UserId = _user, request.OperationId }, cancellationToken: ct))).ShouldBe(tenant);
		var independent = await store.RevokeAllAsync(new GrantRevocationRequest(request.OperationId, _user + "-other",
			GrantRevocationScope.AllTenants, request.RevokedBy), ct);
		independent.RevokedCount.ShouldBe(0);
		(await store.RevokeAllAsync(request, ct)).ShouldBe(first);
	}

	private DomainDb CreateDb() => new(new NpgsqlConnection(fixture.ConnectionString));

	private GrantRevocationRequest Request(GrantRevocationScope scope) => new(Guid.NewGuid(), _user, scope, "operator");

	private Task<int> SaveAsync(PostgresGrantStore store, string tenant, string type, string qualifier, CancellationToken ct) =>
		store.SaveGrantAsync(new Grant(_user, "Name", tenant, type, qualifier, null, "operator", DateTimeOffset.UtcNow), ct);

	private ActivityGroupGrantSnapshot Snapshot(string qualifier) => new([
		new ActivityGroupGrantEntry(_user, "Name", "one", "ActivityGroup", qualifier, null, "operator")]);
}
