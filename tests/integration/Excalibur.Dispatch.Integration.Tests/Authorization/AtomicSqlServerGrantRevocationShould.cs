// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;

using Dapper;

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>Certifies atomic grant revocation and its revision fence against the shipped SQL Server schema.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class AtomicSqlServerGrantRevocationShould(SqlServerFixture fixture) : IAsyncLifetime
{
	private const string EstateLock = "Excalibur.A3.ActivityGroupGrants:authz.[Grant]";
	private readonly string _user = "atomic-" + Guid.NewGuid().ToString("N");
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	/// <inheritdoc />
	public async ValueTask InitializeAsync() => await ShippedGrantSchema.ProvisionSqlServerAsync(
		fixture.ConnectionString, stripBinaryCollation: false, Ct).ConfigureAwait(false);

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	[Fact]
	public async Task RevokeEveryTypeIncludingExpiredGrantsOnlyInTheExactTenant()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		store.GetService(typeof(IAtomicGrantRevocationStore)).ShouldBeSameAs(store);
		foreach (var type in new[] { "Activity", "ActivityGroup", "Custom" })
		{
			_ = await store.SaveGrantAsync(Grant("tenant-A", type, type), Ct).ConfigureAwait(false);
		}
		_ = await store.SaveGrantAsync(Grant("tenant-A", "Activity", "expired", DateTimeOffset.UtcNow.AddDays(-1)), Ct)
			.ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("tenant-a", "ActivityGroup", "other-case"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("tenant-B", "Activity", "other-tenant"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("__untenanted__", "Activity", "untenanted"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("tenant-A", "Activity", "other-user", user: _user + "-other"), Ct)
			.ConfigureAwait(false);

		var result = await store.RevokeAllAsync(Request(GrantRevocationScope.ForTenant("tenant-A")), Ct).ConfigureAwait(false);
		result.RevokedCount.ShouldBe(4);
		result.Revision.ShouldBe(8);
		var snapshot = await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false);
		snapshot.Revision.ShouldBe(result.Revision);
		snapshot.Grants.Select(g => g.Qualifier).Order(StringComparer.Ordinal)
			.ShouldBe(["other-case", "other-tenant", "untenanted"]);
		(await store.GetAllGrantsAsync(_user + "-other", Ct).ConfigureAwait(false)).Count.ShouldBe(1);
		(await ScalarAsync<int>("SELECT COUNT(*) FROM authz.GrantHistory WHERE UserId = @UserId", new { UserId = _user }))
			.ShouldBe(4);
	}

	[Fact]
	public async Task RevokeAllTenantsOnlyWhenExplicitlyRequested()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		_ = await store.SaveGrantAsync(Grant("a", "Activity", "one"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("b", "ActivityGroup", "two"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("__untenanted__", "Custom", "three"), Ct).ConfigureAwait(false);
		var result = await store.RevokeAllAsync(Request(GrantRevocationScope.AllTenants), Ct).ConfigureAwait(false);
		result.RevokedCount.ShouldBe(3);
		(await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false)).Grants.ShouldBeEmpty();
	}

	[Fact]
	public async Task ResolveALostResponseWithTheSameOperationWithoutRevokingLaterGrants()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		_ = await store.SaveGrantAsync(Grant("a", "ActivityGroup", "before"), Ct).ConfigureAwait(false);
		var request = Request(GrantRevocationScope.AllTenants);
		var first = await store.RevokeAllAsync(request, Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("a", "Activity", "after"), Ct).ConfigureAwait(false);
		(await store.RevokeAllAsync(request, Ct).ConfigureAwait(false)).ShouldBe(first);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(first.Revision + 1);
		(await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false)).Grants.Single().Qualifier
			.ShouldBe("after");
		(await ScalarAsync<int>("SELECT COUNT(*) FROM authz.GrantHistory WHERE UserId = @UserId", new { UserId = _user }))
			.ShouldBe(1);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("tenant-A")]
	[InlineData("__untenanted__")]
	public async Task PreserveTheExplicitOperationScopeOnReplay(string? tenant)
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		var selectedTenant = tenant ?? "tenant-A";
		_ = await store.SaveGrantAsync(Grant(selectedTenant, "ActivityGroup", "before"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("tenant-B", "Activity", "other"), Ct).ConfigureAwait(false);
		var request = Request(tenant is null ? GrantRevocationScope.AllTenants : GrantRevocationScope.ForTenant(tenant));
		var first = await store.RevokeAllAsync(request, Ct).ConfigureAwait(false);
		first.RevokedCount.ShouldBe(tenant is null ? 2 : 1);
		_ = await store.SaveGrantAsync(Grant(selectedTenant, "Activity", "after"), Ct).ConfigureAwait(false);
		(await store.RevokeAllAsync(request, Ct).ConfigureAwait(false)).ShouldBe(first);
		var conflicting = new GrantRevocationRequest(request.OperationId, _user,
			tenant is null ? GrantRevocationScope.ForTenant(selectedTenant) : GrantRevocationScope.AllTenants, request.RevokedBy);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(conflicting, Ct)).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(first.Revision + 1);
		(await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false)).Grants.Select(g => g.Qualifier)
			.ShouldContain("after");
		(await ScalarAsync<int>("SELECT COUNT(*) FROM authz.GrantHistory WHERE UserId = @UserId", new { UserId = _user }))
			.ShouldBe(first.RevokedCount);
		(await ScalarAsync<string?>("SELECT ScopeTenantId FROM authz.GrantRevocation WHERE UserId = @UserId AND OperationId = @OperationId",
			new { UserId = _user, request.OperationId })).ShouldBe(tenant);
		var independent = await store.RevokeAllAsync(new GrantRevocationRequest(request.OperationId, _user + "-other",
			GrantRevocationScope.AllTenants, request.RevokedBy), Ct).ConfigureAwait(false);
		independent.RevokedCount.ShouldBe(0);
		(await store.RevokeAllAsync(request, Ct).ConfigureAwait(false)).ShouldBe(first);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RefuseOperationIdentifierReuseWithAnotherPayload(bool changeActor)
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		var request = Request(GrantRevocationScope.AllTenants);
		var first = await store.RevokeAllAsync(request, Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("a", "Activity", "after"), Ct).ConfigureAwait(false);
		var conflicting = new GrantRevocationRequest(request.OperationId, _user,
			changeActor ? GrantRevocationScope.AllTenants : GrantRevocationScope.ForTenant("a"),
			changeActor ? "another-actor" : request.RevokedBy);
		_ = await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(conflicting, Ct)).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(first.Revision + 1);
		(await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false)).Grants.Count.ShouldBe(1);
	}

	[Fact]
	public async Task AdvanceTheRevisionForEveryDirectAndActivityGroupWriter()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(0);
		_ = await store.SaveGrantAsync(Grant("a", "Activity", "one"), Ct).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(1);
		_ = await store.DeleteGrantAsync(_user, "a", "Activity", "one", "actor", DateTimeOffset.UtcNow, Ct)
			.ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(2);
		_ = await store.InsertActivityGroupGrantAsync(_user, "Person", "a", "ActivityGroup", "two", null, "actor", Ct)
			.ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(3);
		_ = await store.DeleteActivityGroupGrantsByUserIdAsync(_user, "ActivityGroup", Ct).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(4);
		_ = await store.ReplaceActivityGroupGrantsForUserAsync(_user, "ActivityGroup", Snapshot(_user, "three"), Ct)
			.ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(5);
		var other = _user + "-new";
		_ = await store.ReplaceActivityGroupGrantsAsync("ActivityGroup", Snapshot(other, "four"), Ct).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(6);
		(await store.ReadUserRevisionAsync(other, Ct).ConfigureAwait(false)).ShouldBe(1);
		_ = await store.DeleteAllActivityGroupGrantsAsync("ActivityGroup", Ct).ConfigureAwait(false);
		(await store.ReadUserRevisionAsync(other, Ct).ConfigureAwait(false)).ShouldBe(2);
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "Trigger identifier and predicate contain only test-generated GUID hex and a fixed prefix; no external input.")]
	public async Task RollBackGrantsAuditAndRevisionWhenTheReceiptCannotCommit()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		_ = await store.SaveGrantAsync(Grant("a", "Activity", "one"), Ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(Grant("a", "ActivityGroup", "two"), Ct).ConfigureAwait(false);
		var trigger = "RejectRevocation_" + Guid.NewGuid().ToString("N");
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(Ct).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = $"CREATE TRIGGER authz.[{trigger}] ON authz.GrantRevocation AFTER INSERT AS "
			+ $"IF EXISTS (SELECT 1 FROM inserted WHERE UserId = N'{_user}') THROW 51002, 'Injected receipt failure', 1;";
		_ = await command.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
		try
		{
			_ = await Should.ThrowAsync<SqlException>(() => store.RevokeAllAsync(Request(GrantRevocationScope.AllTenants), Ct))
				.ConfigureAwait(false);
			var snapshot = await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false);
			snapshot.Revision.ShouldBe(2);
			snapshot.Grants.Count.ShouldBe(2);
			(await ScalarAsync<int>("SELECT COUNT(*) FROM authz.GrantHistory WHERE UserId = @UserId", new { UserId = _user }))
				.ShouldBe(0);
			(await ScalarAsync<int>("SELECT COUNT(*) FROM authz.GrantRevocation WHERE UserId = @UserId", new { UserId = _user }))
				.ShouldBe(0);
		}
		finally
		{
			command.CommandText = $"DROP TRIGGER authz.[{trigger}];";
			_ = await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CancelBeforeCommitWhileWaitingForTheAuthorityLockWithoutChangingState()
	{
		using var db = NewDb();
		var store = new SqlServerGrantStore(db);
		_ = await store.SaveGrantAsync(Grant("a", "ActivityGroup", "before"), Ct).ConfigureAwait(false);
		// Db.Connection opens on access. Capture that connection once, then explicitly close it
		// to exercise the store's opened-here cleanup without accidentally reopening in the assertion.
		var callerConnection = (SqlConnection)db.Connection;
		await callerConnection.CloseAsync().ConfigureAwait(false);
		await using var blocker = new SqlConnection(fixture.ConnectionString);
		await blocker.OpenAsync(Ct).ConfigureAwait(false);
		await using var transaction = await blocker.BeginTransactionAsync(Ct).ConfigureAwait(false);
		var lockResult = await blocker.QuerySingleAsync<int>(new CommandDefinition(
			"DECLARE @r INT; EXEC @r=sp_getapplock @Resource=@Resource, @LockMode='Exclusive', @LockOwner='Transaction'; SELECT @r;",
			new { Resource = EstateLock }, transaction, cancellationToken: Ct)).ConfigureAwait(false);
		lockResult.ShouldBeGreaterThanOrEqualTo(0);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
		cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
		var failure = await Record.ExceptionAsync(() =>
			store.RevokeAllAsync(Request(GrantRevocationScope.AllTenants), cancellation.Token)).ConfigureAwait(false);
		failure.ShouldBeAssignableTo<OperationCanceledException>(failure?.ToString());
		((OperationCanceledException)failure!).CancellationToken.ShouldBe(cancellation.Token);
		cancellation.IsCancellationRequested.ShouldBeTrue();
		callerConnection.State.ShouldBe(ConnectionState.Closed, "the store restores an initially closed caller connection after cancellation");
		await transaction.RollbackAsync(Ct).ConfigureAwait(false);
		var snapshot = await store.ReadUserSnapshotAsync(_user, includeExpired: true, Ct).ConfigureAwait(false);
		snapshot.Revision.ShouldBe(1);
		snapshot.Grants.Count.ShouldBe(1);
	}

	[Fact]
	public async Task SerializeConcurrentSaveAndRevocationAcrossIndependentConnections()
	{
		for (var iteration = 0; iteration < 12; iteration++)
		{
			var user = _user + "-" + iteration;
			using var revokeDb = NewDb();
			using var saveDb = NewDb();
			var revoker = new SqlServerGrantStore(revokeDb);
			var saver = new SqlServerGrantStore(saveDb);
			_ = await revoker.SaveGrantAsync(Grant("a", "ActivityGroup", "before", user: user), Ct).ConfigureAwait(false);
			var revoke = revoker.RevokeAllAsync(new GrantRevocationRequest(Guid.NewGuid(), user,
				GrantRevocationScope.AllTenants, "actor"), Ct);
			var save = saver.SaveGrantAsync(Grant("a", "Activity", "concurrent", user: user), Ct);
			await Task.WhenAll(revoke, save).ConfigureAwait(false);
			var result = await revoke.ConfigureAwait(false);
			var snapshot = await revoker.ReadUserSnapshotAsync(user, includeExpired: true, Ct).ConfigureAwait(false);
			snapshot.Revision.ShouldBe(3);
			result.RevokedCount.ShouldBeOneOf(1, 2);
			snapshot.Grants.Count.ShouldBe(2 - result.RevokedCount,
				"the save commits either before revocation and is removed, or after it and survives");
			if (snapshot.Grants.Count != 0) { snapshot.Grants.Single().Qualifier.ShouldBe("concurrent"); }
		}
	}

	[Fact]
	public async Task RefuseAmbientTransactionsAndPreserveAnOpenCallerConnection()
	{
		using var db = NewDb();
		var connection = (SqlConnection)db.Connection;
		connection.State.ShouldBe(ConnectionState.Open, "DomainDb.Connection opens the connection before returning it");
		var store = new SqlServerGrantStore(db);
		var request = Request(GrantRevocationScope.AllTenants);
		using (var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() => store.RevokeAllAsync(request, Ct)).ConfigureAwait(false);
		}
		(await store.ReadUserRevisionAsync(_user, Ct).ConfigureAwait(false)).ShouldBe(0);
		connection.State.ShouldBe(ConnectionState.Open);
		_ = await store.RevokeAllAsync(request, Ct).ConfigureAwait(false);
		connection.State.ShouldBe(ConnectionState.Open);
	}

	private DomainDb NewDb() => new(new SqlConnection(fixture.ConnectionString));
	private GrantRevocationRequest Request(GrantRevocationScope scope) => new(Guid.NewGuid(), _user, scope, "actor");
	private Grant Grant(string tenant, string type, string qualifier, DateTimeOffset? expires = null, string? user = null) =>
		new(user ?? _user, "Person", tenant, type, qualifier, expires, "actor", DateTimeOffset.UtcNow);
	private static ActivityGroupGrantSnapshot Snapshot(string user, string qualifier) =>
		new([new ActivityGroupGrantEntry(user, "Person", "a", "ActivityGroup", qualifier, null, "actor")]);
	private async Task<T> ScalarAsync<T>(string sql, object parameters)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		return await connection.QuerySingleAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: Ct)).ConfigureAwait(false);
	}
}
