// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Data.SqlClient;

using MsOptions = Microsoft.Extensions.Options.Options;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Exercises each activity-group store against the table its package SHIPS, provisioned from the shipped
/// script and never from a definition restated here.
/// </summary>
/// <remarks>
/// <para>
/// Before this suite no test created the activity-group table, so neither store had ever run against a
/// real database -- and a store that has never run is one whose SQL nobody has checked. Every arm here
/// goes through the real store, over a real connection, to the schema a consumer applies.
/// </para>
/// <para>
/// The table's key is (tenant, group name, activity), with the tenant in the key rather than merely a
/// column. Two arms depend on exactly that: one proves two tenants may each own a group of the same name,
/// the other proves the same triple cannot be written twice. Drop the tenant from the key and the first
/// fails; drop the key entirely and the second does.
/// </para>
/// </remarks>
public abstract class ActivityGroupStoreShippedSchemaShould : IAsyncLifetime
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";

	/// <summary>Provisions the table from the shipped script.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table exists.</returns>
	protected abstract Task ProvisionAsync(CancellationToken cancellationToken);

	/// <summary>Empties the table so each arm counts only its own rows.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table is empty.</returns>
	protected abstract Task TruncateAsync(CancellationToken cancellationToken);

	/// <summary>Creates the store under test over a fresh connection.</summary>
	/// <returns>The store and the database it owns, which the caller disposes.</returns>
	protected abstract (IActivityGroupStore Store, IDisposable Db) CreateStore();

	/// <summary>Reads the key columns' DECLARED widths, in characters, from the provisioned table.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The declared widths of the tenant, name and activity columns.</returns>
	protected abstract Task<(int Tenant, int Name, int Activity)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken);

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		var ct = TestContext.Current.CancellationToken;
		await ProvisionAsync(ct).ConfigureAwait(false);
		await TruncateAsync(ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <summary>LIVENESS: a group written through the store reads back with every activity it confers.</summary>
	[Fact]
	public async Task RoundTripAGroupThroughTheShippedSchema()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		_ = await store.CreateActivityGroupAsync(TenantA, "Admins", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantA, "Admins", "Write", ct).ConfigureAwait(false);

		(await store.ActivityGroupExistsAsync(TenantA, "Admins", ct).ConfigureAwait(false))
			.ShouldBeTrue("a group written through the store must be found by name in its own tenant");

		var groups = await store.FindActivityGroupsAsync(TenantA, ct).ConfigureAwait(false);
		groups.Count.ShouldBe(1, "two activities of one group are one group, not two");
		groups.Values.Single().OrderBy(static a => a, StringComparer.Ordinal).ToArray()
			.ShouldBe(["Read", "Write"], customMessage: "every activity the group confers must read back");
	}

	/// <summary>
	/// SAFETY, and the property the key exists for: two tenants may each own a group of the same name, and
	/// neither can read the other's.
	/// </summary>
	[Fact]
	public async Task KeepTwoTenantsSameNamedGroupsApart()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		// The SAME (group, activity) pair in two tenants is the collision the key must allow: with the tenant
		// outside the key, tenant B's first write is refused as a duplicate of tenant A's. Each tenant also
		// holds one activity the other does not, so the reads below can prove isolation, not just presence.
		_ = await store.CreateActivityGroupAsync(TenantA, "Administrators", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantA, "Administrators", "Write", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantB, "Administrators", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantB, "Administrators", "Delete", ct).ConfigureAwait(false);

		var a = await store.FindActivityGroupsAsync(TenantA, ct).ConfigureAwait(false);
		var b = await store.FindActivityGroupsAsync(TenantB, ct).ConfigureAwait(false);

		a.Values.SelectMany(static x => x).OrderBy(static x => x, StringComparer.Ordinal).ToArray().ShouldBe(
			["Read", "Write"], customMessage: "tenant A must see only its own group's activities, never tenant B's");
		b.Values.SelectMany(static x => x).OrderBy(static x => x, StringComparer.Ordinal).ToArray().ShouldBe(
			["Delete", "Read"], customMessage: "tenant B must see only its own group's activities, never tenant A's");
	}

	/// <summary>SAFETY: deleting one tenant's groups leaves another tenant's alone.</summary>
	[Fact]
	public async Task DeleteOneTenantsGroupsWithoutTouchingAnothers()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		_ = await store.CreateActivityGroupAsync(TenantA, "Admins", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantB, "Admins", "Read", ct).ConfigureAwait(false);

		var removed = await store.DeleteActivityGroupsForTenantAsync(TenantA, ct).ConfigureAwait(false);

		removed.ShouldBe(1, "exactly tenant A's one row");
		(await store.ActivityGroupExistsAsync(TenantA, "Admins", ct).ConfigureAwait(false)).ShouldBeFalse();
		(await store.ActivityGroupExistsAsync(TenantB, "Admins", ct).ConfigureAwait(false))
			.ShouldBeTrue("deleting tenant A's groups must not remove tenant B's");
	}

	/// <summary>
	/// LIVENESS: the whole-catalogue replace reports every tenant that held groups before it, read under the
	/// same lock that orders it.
	/// </summary>
	[Fact]
	public async Task ReportEveryTenantTheReplaceRemoved()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		_ = await store.CreateActivityGroupAsync(TenantA, "Admins", "Read", ct).ConfigureAwait(false);
		_ = await store.CreateActivityGroupAsync(TenantB, "Admins", "Read", ct).ConfigureAwait(false);

		var previous = await store.ReplaceAllActivityGroupsAsync(
			new ActivityGroupCatalogue([new ActivityGroupEntry("tenant-c", "Admins", "Read")]), ct).ConfigureAwait(false);

		previous.Distinct(StringComparer.Ordinal).OrderBy(static t => t, StringComparer.Ordinal).ToArray()
			.ShouldBe([TenantA, TenantB], customMessage: "every tenant whose groups were removed must be reported");
		(await store.FindActivityGroupsAsync(TenantA, ct).ConfigureAwait(false)).ShouldBeEmpty();
		(await store.FindActivityGroupsAsync(TenantB, ct).ConfigureAwait(false)).ShouldBeEmpty();
		(await store.FindActivityGroupsAsync("tenant-c", ct).ConfigureAwait(false)).ShouldHaveSingleItem();
	}

	/// <summary>
	/// SAFETY: the same (tenant, group, activity) triple cannot be written twice -- which is the only thing
	/// here that proves the shipped script actually declares a key.
	/// </summary>
	[Fact]
	public async Task RefuseTheSameTripleTwice()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		_ = await store.CreateActivityGroupAsync(TenantA, "Admins", "Read", ct).ConfigureAwait(false);

		_ = await Should.ThrowAsync<DbException>(
			async () => await store.CreateActivityGroupAsync(TenantA, "Admins", "Read", ct).ConfigureAwait(false));
	}

	/// <summary>
	/// The key is declared at widths both providers share, and on SQL Server those widths fit its 900-byte
	/// limit on a clustered key.
	/// </summary>
	/// <remarks>
	/// Read from the DATABASE, not from the script text: this is the table a consumer's store talks to. It
	/// is the only arm that can see a script regress to a wider name column, because the store's own guard
	/// keeps every row it writes inside the narrower limit, so no insertion could ever reach the old one.
	/// </remarks>
	[Fact]
	public async Task DeclareTheSameKeyWidthsOnBothProviders_WithinSqlServersKeyLimit()
	{
		var (tenant, name, activity) = await ReadDeclaredKeyWidthsAsync(TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		(tenant, name, activity).ShouldBe(
			(64, 128, 256),
			"both providers must declare the same key widths, or the same name is accepted by one and refused by the other");
		((tenant + name + activity) * 2).ShouldBeLessThanOrEqualTo(
			900,
			"SQL Server stores these as two bytes per character and refuses any row whose clustered key exceeds 900 bytes");
	}

	/// <summary>LIVENESS at the limit: the longest names the schema declares are accepted and read back.</summary>
	[Fact]
	public async Task AcceptTheLongestNamesTheSchemaDeclares()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		var tenant = new string('t', 64);
		var group = new string('g', 128);
		var activity = new string('a', 256);

		_ = await store.CreateActivityGroupAsync(tenant, group, activity, ct).ConfigureAwait(false);

		(await store.ActivityGroupExistsAsync(tenant, group, ct).ConfigureAwait(false))
			.ShouldBeTrue("a name exactly at the declared limit must be stored, not rejected");
		(await store.FindActivityGroupsAsync(tenant, ct).ConfigureAwait(false))
			.Values.SelectMany(static x => x).ShouldBe([activity], customMessage: "the full-length activity must read back intact");
	}

	/// <summary>SAFETY past the limit: an over-length group name is refused by the store, naming the parameter.</summary>
	[Fact]
	public async Task RejectAnOverLengthGroupName_BeforeTheDatabaseDoes()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		var thrown = await Should.ThrowAsync<ArgumentException>(
			async () => await store.CreateActivityGroupAsync(TenantA, new string('g', 129), "Read", ct).ConfigureAwait(false));

		thrown.ParamName.ShouldBe("name");
	}

	/// <summary>SAFETY past the limit: an over-length activity name is refused by the store, naming the parameter.</summary>
	[Fact]
	public async Task RejectAnOverLengthActivityName_BeforeTheDatabaseDoes()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		var thrown = await Should.ThrowAsync<ArgumentException>(
			async () => await store.CreateActivityGroupAsync(TenantA, "Admins", new string('a', 257), ct).ConfigureAwait(false));

		thrown.ParamName.ShouldBe("activityName");
	}
}

/// <summary>Runs the activity-group arms against the SQL Server store and its shipped script.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerActivityGroupStoreShippedSchemaShould(SqlServerFixture fixture)
	: ActivityGroupStoreShippedSchemaShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.ProvisionSqlServerAsync(fixture.ConnectionString, cancellationToken);

	/// <inheritdoc />
	protected override Task TruncateAsync(CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.TruncateSqlServerAsync(fixture.ConnectionString, cancellationToken);

	/// <inheritdoc />
	protected override (IActivityGroupStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new SqlConnection(fixture.ConnectionString));
		return (new SqlServerActivityGroupStore(db, MsOptions.Create(new SqlServerAuthorizationOptions())), db);
	}

	/// <inheritdoc />
	/// <remarks><c>sys.columns.max_length</c> is in BYTES; NVARCHAR stores two per character.</remarks>
	protected override async Task<(int Tenant, int Name, int Activity)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT name, max_length FROM sys.columns WHERE object_id = OBJECT_ID(N'authz.ActivityGroup')";

		var widths = new Dictionary<string, int>(StringComparer.Ordinal);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			widths[reader.GetString(0)] = reader.GetInt16(1) / 2;
		}

		return (widths["TenantId"], widths["Name"], widths["ActivityName"]);
	}
}

/// <summary>Runs the activity-group arms against the Postgres store and its shipped script.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresActivityGroupStoreShippedSchemaShould(PostgresFixture fixture)
	: ActivityGroupStoreShippedSchemaShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.ProvisionPostgresAsync(fixture.ConnectionString, cancellationToken);

	/// <inheritdoc />
	protected override Task TruncateAsync(CancellationToken cancellationToken) =>
		ShippedActivityGroupSchema.TruncatePostgresAsync(fixture.ConnectionString, cancellationToken);

	/// <inheritdoc />
	protected override (IActivityGroupStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		return (new PostgresActivityGroupStore(db, MsOptions.Create(new PostgresAuthorizationOptions())), db);
	}

	/// <inheritdoc />
	/// <remarks><c>character_maximum_length</c> is already in characters.</remarks>
	protected override async Task<(int Tenant, int Name, int Activity)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT column_name, character_maximum_length FROM information_schema.columns "
			+ "WHERE table_schema = 'authz' AND table_name = 'activity_group'";

		var widths = new Dictionary<string, int>(StringComparer.Ordinal);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			widths[reader.GetString(0)] = reader.GetInt32(1);
		}

		return (widths["tenant_id"], widths["name"], widths["activity_name"]);
	}
}
