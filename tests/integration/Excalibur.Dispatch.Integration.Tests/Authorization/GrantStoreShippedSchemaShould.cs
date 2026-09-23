// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Data.SqlClient;

using Npgsql;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Exercises every grant-store operation against the tables its package SHIPS, provisioned from the shipped
/// script and never from a definition restated here.
/// </summary>
/// <remarks>
/// Before this suite no test created the grant tables, so neither SQL grant store had ever run against a real
/// database. Every arm goes through the real store, over a real connection, to the schema a consumer applies.
/// Every arm writes under identifiers unique to it, so arms need no truncation and cannot see each other.
/// </remarks>
public abstract class GrantStoreShippedSchemaShould : IAsyncLifetime
{
	private readonly string _run = Guid.NewGuid().ToString("N")[..10];

	/// <summary>Provisions the tables from the shipped script.</summary>
	protected abstract Task ProvisionAsync(CancellationToken cancellationToken);

	/// <summary>Creates the store under test over a fresh connection.</summary>
	/// <returns>The store and the database it owns, which the caller disposes.</returns>
	protected abstract (IGrantStore Store, IDisposable Db) CreateStore();

	/// <summary>Counts the history rows recorded for one grant.</summary>
	protected abstract Task<int> CountHistoryAsync(string userId, CancellationToken cancellationToken);

	/// <summary>Reads the DECLARED widths, in characters, of the four key columns.</summary>
	protected abstract Task<(int User, int Tenant, int Type, int Qualifier)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken);

	/// <inheritdoc />
	public async ValueTask InitializeAsync() =>
		await ProvisionAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private string User(string name) => $"{name}-{_run}";

	private string Tenant => $"tenant-{_run}";

	private Grant NewGrant(string user, string qualifier, DateTimeOffset? expiresOn = null, string grantedBy = "admin") =>
		new(user, "A Person", Tenant, "Activity", qualifier, expiresOn, grantedBy,
			DateTimeOffset.UtcNow.AddMinutes(-5));

	/// <summary>LIVENESS: a saved grant reads back through every read the store offers.</summary>
	[Fact]
	public async Task RoundTripAGrantThroughEveryRead()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		var user = User("alice");

		(await store.SaveGrantAsync(NewGrant(user, "orders.read"), ct).ConfigureAwait(false)).ShouldBe(1);

		var read = await store.GetGrantAsync(user, Tenant, "Activity", "orders.read", ct).ConfigureAwait(false);
		_ = read.ShouldNotBeNull("a saved grant must be found by its four identity values");
		read.GrantedBy.ShouldBe("admin");
		read.FullName.ShouldBe("A Person");

		(await store.GrantExistsAsync(user, Tenant, "Activity", "orders.read", ct).ConfigureAwait(false)).ShouldBeTrue();
		(await store.GetAllGrantsAsync(user, ct).ConfigureAwait(false)).Count.ShouldBe(1);

		var query = (IGrantQueryStore)store.GetService(typeof(IGrantQueryStore))!;
		(await query.FindUserGrantsAsync(user, ct).ConfigureAwait(false)).Count.ShouldBe(1);
	}

	/// <summary>
	/// SAVE IS AN UPSERT, as <see cref="IGrantStore.SaveGrantAsync"/> promises: saving an existing grant replaces
	/// its details. Against the key the shipped script declares, a plain INSERT fails here instead.
	/// </summary>
	[Fact]
	public async Task ReplaceAGrantsDetails_WhenTheSameGrantIsSavedAgain()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		var user = User("bob");
		var later = DateTimeOffset.UtcNow.AddDays(30);

		_ = await store.SaveGrantAsync(NewGrant(user, "orders.read"), ct).ConfigureAwait(false);
		(await store.SaveGrantAsync(NewGrant(user, "orders.read", later, grantedBy: "second-admin"), ct).ConfigureAwait(false))
			.ShouldBe(1, "a re-grant touches exactly one row");

		var all = await store.GetAllGrantsAsync(user, ct).ConfigureAwait(false);
		all.Count.ShouldBe(1, "a re-grant must not create a second row");
		all[0].GrantedBy.ShouldBe("second-admin", "a re-grant must replace the grant's details");
		all[0].ExpiresOn.ShouldNotBeNull().ShouldBe(later, TimeSpan.FromSeconds(1));
	}

	/// <summary>
	/// A revocation removes the grant and records it in history; a grant revoked, re-granted and revoked again
	/// has two history rows, which a natural key on history would refuse.
	/// </summary>
	[Fact]
	public async Task RecordEveryRevocationInHistory()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		var user = User("carol");

		_ = await store.SaveGrantAsync(NewGrant(user, "orders.read"), ct).ConfigureAwait(false);
		_ = await store.DeleteGrantAsync(user, Tenant, "Activity", "orders.read", "auditor", DateTimeOffset.UtcNow, ct)
			.ConfigureAwait(false);

		(await store.GetGrantAsync(user, Tenant, "Activity", "orders.read", ct).ConfigureAwait(false))
			.ShouldBeNull("a revoked grant must no longer be in force");
		(await CountHistoryAsync(user, ct).ConfigureAwait(false)).ShouldBe(1, "the revocation must be recorded");

		_ = await store.SaveGrantAsync(NewGrant(user, "orders.read"), ct).ConfigureAwait(false);
		_ = await store.DeleteGrantAsync(user, Tenant, "Activity", "orders.read", "auditor", DateTimeOffset.UtcNow, ct)
			.ConfigureAwait(false);

		(await CountHistoryAsync(user, ct).ConfigureAwait(false)).ShouldBe(2, "each revocation is its own history row");
	}

	/// <summary>
	/// An expired grant is not in force: it is excluded from the active reads and the existence check, and
	/// returned only when expired grants are explicitly asked for.
	/// </summary>
	[Fact]
	public async Task ExcludeAnExpiredGrantFromActiveReads()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		var user = User("dave");

		_ = await store.SaveGrantAsync(NewGrant(user, "expired", DateTimeOffset.UtcNow.AddMinutes(-1)), ct).ConfigureAwait(false);
		_ = await store.SaveGrantAsync(NewGrant(user, "current", DateTimeOffset.UtcNow.AddMinutes(10)), ct).ConfigureAwait(false);

		(await store.GrantExistsAsync(user, Tenant, "Activity", "expired", ct).ConfigureAwait(false))
			.ShouldBeFalse("a grant that expired a minute ago must not authorize");
		(await store.GrantExistsAsync(user, Tenant, "Activity", "current", ct).ConfigureAwait(false))
			.ShouldBeTrue("a grant expiring in ten minutes must still authorize");

		(await store.GetAllGrantsAsync(user, ct).ConfigureAwait(false)).Select(g => g.Qualifier)
			.ShouldBe(["current"], customMessage: "the active read must exclude the expired grant");
		(await store.GetAllGrantsAsync(user, includeExpired: true, ct).ConfigureAwait(false)).Count
			.ShouldBe(2, "an explicit request for expired grants must include them");
	}

	/// <summary>
	/// The widest value the schema declares, in every key column at once, is accepted and reads back intact --
	/// so the key's declared widths are usable together, within SQL Server's 900-byte key limit.
	/// </summary>
	[Fact]
	public async Task AcceptTheLongestValuesTheSchemaDeclares()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		var user = (_run + new string('u', 128))[..128];
		var tenant = (_run + new string('t', 64))[..64];
		var type = new string('g', 64);
		var qualifier = new string('q', 192);

		_ = await store.SaveGrantAsync(
			new Grant(user, "A Person", tenant, type, qualifier, null, "admin", DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

		_ = (await store.GetGrantAsync(user, tenant, type, qualifier, ct).ConfigureAwait(false))
			.ShouldNotBeNull("a grant at the declared maximum width in every key column must round-trip");
	}

	/// <summary>An over-length value is refused by the store, naming the field, before the database sees it.</summary>
	[Theory]
	[InlineData("UserId", 129, 0, 0, 0)]
	[InlineData("TenantId", 0, 65, 0, 0)]
	[InlineData("GrantType", 0, 0, 65, 0)]
	[InlineData("Qualifier", 0, 0, 0, 193)]
	public async Task RejectAnOverLengthValue_BeforeTheDatabaseDoes(string field, int user, int tenant, int type, int qualifier)
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;

		static string Of(int length, string fallback) => length == 0 ? fallback : new string('x', length);

		var thrown = await Should.ThrowAsync<ArgumentException>(() => store.SaveGrantAsync(
			new Grant(Of(user, User("eve")), null, Of(tenant, Tenant), Of(type, "Activity"), Of(qualifier, "q"), null,
				"admin", DateTimeOffset.UtcNow), ct));

		thrown.ParamName.ShouldNotBeNull().ShouldEndWith(field);
	}

	/// <summary>
	/// The activity-group grant operations write and read the SAME table as every other operation. The Postgres
	/// insert once wrote a quoted mixed-case relation that the shipped schema does not have.
	/// </summary>
	[Fact]
	public async Task RunTheActivityGroupGrantOperationsAgainstTheSameTable()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, db) = CreateStore();
		using var ownedDb = db;
		var agStore = (IActivityGroupGrantStore)store.GetService(typeof(IActivityGroupGrantStore))!;
		var groupType = "ActivityGroup-" + _run;
		var user = User("frank");

		(await agStore.InsertActivityGroupGrantAsync(user, "Frank", Tenant, groupType, "Admins", null, "sync", ct)
			.ConfigureAwait(false)).ShouldBe(1);

		(await agStore.GetDistinctActivityGroupGrantUserIdsAsync(groupType, ct).ConfigureAwait(false))
			.ShouldBe([user], customMessage: "the inserted activity-group grant must be read back by type");
		_ = (await store.GetGrantAsync(user, Tenant, groupType, "Admins", ct).ConfigureAwait(false))
			.ShouldNotBeNull("an activity-group grant must land in the table the grant reads use");

		(await agStore.DeleteActivityGroupGrantsByUserIdAsync(user, groupType, ct).ConfigureAwait(false)).ShouldBe(1);
		_ = await agStore.InsertActivityGroupGrantAsync(user, "Frank", Tenant, groupType, "Admins", null, "sync", ct)
			.ConfigureAwait(false);
		(await agStore.DeleteAllActivityGroupGrantsAsync(groupType, ct).ConfigureAwait(false)).ShouldBe(1);
		(await agStore.GetDistinctActivityGroupGrantUserIdsAsync(groupType, ct).ConfigureAwait(false)).ShouldBeEmpty();
	}

	/// <summary>
	/// The key columns are declared at the widths the store enforces, and on SQL Server the four together fit
	/// its 900-byte key limit.
	/// </summary>
	[Fact]
	public async Task DeclareTheKeyWidthsTheStoreEnforces()
	{
		var widths = await ReadDeclaredKeyWidthsAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		widths.ShouldBe((128, 64, 64, 192));
		((widths.User + widths.Tenant + widths.Type + widths.Qualifier) * 2).ShouldBeLessThanOrEqualTo(900,
			"SQL Server stores NVARCHAR at two bytes per character and rejects a clustered key wider than 900 bytes");
	}
}

/// <summary>Runs the grant-store arms against the SQL Server store and its shipped script.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerGrantStoreShippedSchemaShould(SqlServerFixture fixture) : GrantStoreShippedSchemaShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(CancellationToken cancellationToken) =>
		ShippedGrantSchema.ProvisionSqlServerAsync(fixture.ConnectionString, stripBinaryCollation: false, cancellationToken);

	/// <inheritdoc />
	protected override (IGrantStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new SqlConnection(fixture.ConnectionString));
		return (new SqlServerGrantStore(db), db);
	}

	/// <inheritdoc />
	protected override async Task<int> CountHistoryAsync(string userId, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM authz.GrantHistory WHERE UserId = @UserId";
		_ = command.Parameters.AddWithValue("@UserId", userId);
		return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
	}

	/// <inheritdoc />
	/// <remarks><c>sys.columns.max_length</c> is in BYTES; NVARCHAR stores two per character.</remarks>
	protected override async Task<(int User, int Tenant, int Type, int Qualifier)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT name, max_length FROM sys.columns WHERE object_id = OBJECT_ID(N'authz.Grant')";

		var widths = new Dictionary<string, int>(StringComparer.Ordinal);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			widths[reader.GetString(0)] = reader.GetInt16(1) / 2;
		}

		return (widths["UserId"], widths["TenantId"], widths["GrantType"], widths["Qualifier"]);
	}
}

/// <summary>Runs the grant-store arms against the Postgres store and its shipped script.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresGrantStoreShippedSchemaShould(PostgresFixture fixture) : GrantStoreShippedSchemaShould
{
	/// <inheritdoc />
	protected override Task ProvisionAsync(CancellationToken cancellationToken) =>
		ShippedGrantSchema.ProvisionPostgresAsync(fixture.ConnectionString, cancellationToken);

	/// <inheritdoc />
	protected override (IGrantStore Store, IDisposable Db) CreateStore()
	{
		var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		return (new PostgresGrantStore(db), db);
	}

	/// <inheritdoc />
	protected override async Task<int> CountHistoryAsync(string userId, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT COUNT(*) FROM authz.grant_history WHERE user_id = @UserId";
		_ = command.Parameters.AddWithValue("UserId", userId);
		return (int)(long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
	}

	/// <inheritdoc />
	/// <remarks><c>character_maximum_length</c> is already in characters.</remarks>
	protected override async Task<(int User, int Tenant, int Type, int Qualifier)> ReadDeclaredKeyWidthsAsync(
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(fixture.ConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT column_name, character_maximum_length FROM information_schema.columns "
			+ "WHERE table_schema = 'authz' AND table_name = 'grant'";

		var widths = new Dictionary<string, int>(StringComparer.Ordinal);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!reader.IsDBNull(1))
			{
				widths[reader.GetString(0)] = reader.GetInt32(1);
			}
		}

		return (widths["user_id"], widths["tenant_id"], widths["grant_type"], widths["qualifier"]);
	}

	/// <summary>
	/// Expiry is correct on a server whose session is NOT in UTC. The store compared a timestamptz column with
	/// <c>now() at time zone 'utc'</c> -- a timestamp WITHOUT zone, which Postgres reinterprets in the session's
	/// zone -- so on a session fourteen hours ahead a grant that expired a minute ago still authorized.
	/// </summary>
	[Fact]
	public async Task EvaluateExpiryCorrectly_WhenTheSessionIsNotInUtc()
	{
		var ct = TestContext.Current.CancellationToken;
		var nonUtc = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Timezone = "Pacific/Kiritimati" }
			.ConnectionString;
		var db = new DomainDb(new NpgsqlConnection(nonUtc));
		using var ownedDb = db;
		var store = new PostgresGrantStore(db);
		var user = "tz-" + Guid.NewGuid().ToString("N")[..10];
		var tenant = "tenant-" + user;

		_ = await store.SaveGrantAsync(
			new Grant(user, null, tenant, "Activity", "expired", DateTimeOffset.UtcNow.AddMinutes(-1), "admin", DateTimeOffset.UtcNow),
			ct).ConfigureAwait(false);

		(await store.GrantExistsAsync(user, tenant, "Activity", "expired", ct).ConfigureAwait(false))
			.ShouldBeFalse("a grant that expired a minute ago must not authorize, whatever the session's time zone");
		(await store.GetAllGrantsAsync(user, ct).ConfigureAwait(false))
			.ShouldBeEmpty("the active read must exclude it too");
	}
}

/// <summary>Holds the grant-query contract against the SQL Server store on its shipped table.</summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerGrantQueryStoreConformanceShould(SqlServerFixture fixture) : GrantQueryStoreConformanceTestBase
{
	/// <inheritdoc />
	protected override async Task<IGrantStore> CreateStoreAsync()
	{
		await ShippedGrantSchema.ProvisionSqlServerAsync(
			fixture.ConnectionString, stripBinaryCollation: false, TestContext.Current.CancellationToken).ConfigureAwait(false);
		return new SqlServerGrantStore(new DomainDb(new SqlConnection(fixture.ConnectionString)));
	}
}

/// <summary>
/// Holds the grant-query contract against the SQL Server store on a table a consumer created by hand in a
/// CASE-INSENSITIVE database -- the shipped script with its binary collation removed. The store's own
/// comparisons must stay exact there: in authorization 'Admin' is not 'admin'.
/// </summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class SqlServerGrantQueryStoreOnACaseInsensitiveTableConformanceShould(SqlServerFixture fixture)
	: GrantQueryStoreConformanceTestBase
{
	/// <inheritdoc />
	protected override async Task<IGrantStore> CreateStoreAsync()
	{
		var ct = TestContext.Current.CancellationToken;
		var connectionString = await ShippedGrantSchema.EnsureCaseInsensitiveSqlServerDatabaseAsync(
			fixture.ConnectionString, ct).ConfigureAwait(false);
		await ShippedGrantSchema.ProvisionSqlServerAsync(connectionString, stripBinaryCollation: true, ct).ConfigureAwait(false);
		return new SqlServerGrantStore(new DomainDb(new SqlConnection(connectionString)));
	}
}

/// <summary>Holds the grant-query contract against the Postgres store on its shipped table.</summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class PostgresGrantQueryStoreConformanceShould(PostgresFixture fixture) : GrantQueryStoreConformanceTestBase
{
	/// <inheritdoc />
	protected override async Task<IGrantStore> CreateStoreAsync()
	{
		await ShippedGrantSchema.ProvisionPostgresAsync(fixture.ConnectionString, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		return new PostgresGrantStore(new DomainDb(new NpgsqlConnection(fixture.ConnectionString)));
	}
}
