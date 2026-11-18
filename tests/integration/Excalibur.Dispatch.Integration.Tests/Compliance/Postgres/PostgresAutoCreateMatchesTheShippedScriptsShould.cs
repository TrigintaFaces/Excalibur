// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Testing.Conformance;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// The two ways a consumer can provision this schema must produce the same shape: the store's own
/// auto-create DDL and the shipped scripts.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DIVERGENCE THIS CATCHES ALREADY SHIPPED ONCE.</b> The auto-create CREATE TABLE omitted a
/// column that the store's own INSERT, its conflict update and two of its SELECTs all bind. The table was
/// created successfully and then failed on the first registration write with a raw driver error naming an
/// absent column. A consumer who provisioned by running the shipped scripts was unaffected, which is what
/// made the defect easy to misread as a test-fixture problem.
/// </para>
/// <para>
/// <b>Fixing that once did not make it unrepeatable, which is why this arm exists.</b> The auto-create
/// DDL and the shipped script are separate literals with no shared source, so nothing prevents the next
/// column from being added to the statements and one of the two. The startup shape verifier does not
/// close it either: it checks the columns on its own required list, so a column added to the statements
/// but to neither the DDL nor that list is invisible to it. Three copies of one fact, and a verifier that
/// can only compare two of them.
/// </para>
/// <para>
/// <b>THE ENGINE IS THE ORACLE, not a list kept in this file.</b> A list here would be a fourth copy of
/// the same fact, free to drift from all three — and drift between copies of a provisioning fact is the
/// defect under test. So this arm provisions one database each way and asks Postgres to describe both,
/// then compares. Adding a column correctly to both paths keeps it green with no edit here; adding it to
/// one reddens it and names the column.
/// </para>
/// <para>
/// <b>Two databases, and that is load-bearing rather than tidiness.</b> Both paths create the shipped
/// default table names, so provisioning them into one database would mean the second path found the
/// tables already present and created nothing — and the comparison would be of one schema with itself,
/// which passes unconditionally.
/// </para>
/// <para>
/// <b>Scoped to the tables BOTH paths create.</b> The shipped script set provisions the whole compliance
/// schema while this store's auto-create builds only its own two tables, so a whole-database comparison
/// would report every other table as a difference and say nothing about agreement. The intersection is
/// the sound comparison; the arm fails if that intersection is empty, so a store that creates nothing
/// cannot pass vacuously.
/// </para>
/// <para>
/// Docker is a hard requirement rather than a skip. The property is what a real engine reports about two
/// real schemas, and a skip would report success having provisioned nothing.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresAutoCreateMatchesTheShippedScriptsShould : IAsyncLifetime
{
	private readonly PostgresFixture _fixture;
	private readonly string _scriptedDatabase = $"provision_scripted_{Guid.NewGuid():N}";
	private readonly string _autoCreatedDatabase = $"provision_autocreated_{Guid.NewGuid():N}";
	private string _scriptedConnectionString = string.Empty;
	private string _autoCreatedConnectionString = string.Empty;

	public PostgresAutoCreateMatchesTheShippedScriptsShould(PostgresFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		_scriptedConnectionString = await CreateDatabaseAsync(_scriptedDatabase).ConfigureAwait(true);
		_autoCreatedConnectionString = await CreateDatabaseAsync(_autoCreatedDatabase).ConfigureAwait(true);
	}

	public async ValueTask DisposeAsync()
	{
		await DropDatabaseAsync(_autoCreatedDatabase, _autoCreatedConnectionString).ConfigureAwait(true);
		await DropDatabaseAsync(_scriptedDatabase, _scriptedConnectionString).ConfigureAwait(true);
	}

	/// <summary>
	/// Both provisioning paths produce the same columns for every table they both create.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task ProduceTheSameColumns_AsTheShippedScripts()
	{
		var token = TestContext.Current.CancellationToken;

		await ShippedCompliancePostgresSchema.EnsureCreatedAsync(_scriptedConnectionString, token)
			.ConfigureAwait(true);

		// Any operation forces initialization, and the auto-create DDL runs there. A read is the cheapest
		// trigger and needs no fixture data.
		//
		// ITS FAILURE IS DELIBERATELY NOT THIS ARM'S VERDICT, and that is what keeps the comparison below
		// the discriminator rather than a decoration. Measured: with the original defect restored, the
		// trigger read throws before any schema is described -- the shape verifier rejects the table, or
		// the SELECT binds the absent column and the driver does -- so an arm that let the trigger decide
		// would be red for the right class of reason while never comparing anything, and would report the
		// same red for a connection fault. The exception is held and surfaced only if the comparison finds
		// nothing, so a trigger that failed for an unrelated reason still cannot pass silently.
		Exception? triggerFailure = null;
		try
		{
			var store = CreateAutoCreatingStore();
			_ = await store.GetAllRegistrationsAsync(token).ConfigureAwait(true);
		}
		catch (Exception ex)
		{
			triggerFailure = ex;
		}

		var scripted = await DescribeAsync(_scriptedConnectionString, token).ConfigureAwait(true);
		var autoCreated = await DescribeAsync(_autoCreatedConnectionString, token).ConfigureAwait(true);

		var shared = autoCreated.Keys.Where(scripted.ContainsKey).OrderBy(t => t, StringComparer.Ordinal).ToList();

		shared.ShouldNotBeEmpty(
			"the two paths must create at least one table in common, or this arm compares nothing and "
			+ $"passes unconditionally. Auto-create produced [{string.Join(", ", autoCreated.Keys.Order(StringComparer.Ordinal))}] "
			+ "and the shipped scripts produced "
			+ $"[{string.Join(", ", scripted.Keys.Order(StringComparer.Ordinal))}]");

		foreach (var table in shared)
		{
			var missingFromAutoCreate = scripted[table].Except(autoCreated[table], StringComparer.Ordinal)
				.OrderBy(c => c, StringComparer.Ordinal).ToList();
			var missingFromScripts = autoCreated[table].Except(scripted[table], StringComparer.Ordinal)
				.OrderBy(c => c, StringComparer.Ordinal).ToList();

			missingFromAutoCreate.ShouldBeEmpty(
				$"table '{table}' provisioned by the store's auto-create DDL is missing "
				+ $"[{string.Join(", ", missingFromAutoCreate)}], which the shipped scripts do create. A "
				+ "consumer who sets AutoCreateSchema gets a table the store's own statements cannot write "
				+ "to, and the failure is a raw driver error on their first write rather than anything this "
				+ "framework reports");

			missingFromScripts.ShouldBeEmpty(
				$"table '{table}' provisioned by the shipped scripts is missing "
				+ $"[{string.Join(", ", missingFromScripts)}], which the store's auto-create DDL does create. "
				+ "A consumer who provisions by script — the documented default, because an application "
				+ "connection holding DDL rights is a posture regulated environments refuse — gets the "
				+ "narrower table, so the defect lands on the path most of them take");
		}

		// Reached only when the two schemas agree. If the trigger nevertheless failed, the cause is not a
		// shape divergence and must not be swallowed: a connection fault, a permission problem, or a
		// provisioning error this comparison cannot see.
		if (triggerFailure is not null)
		{
			throw new InvalidOperationException(
				"The two provisioning paths produced identical columns for every shared table, so this "
				+ "arm's own subject holds -- but the operation that triggers auto-creation still failed, "
				+ "for a reason that is NOT a shape divergence. Surfaced rather than swallowed because a "
				+ "green here would otherwise mean only that nothing could be compared.",
				triggerFailure);
		}
	}

	/// <summary>Asks the engine for every table and its columns, so no list here can drift.</summary>
	private static async Task<Dictionary<string, HashSet<string>>> DescribeAsync(
		string connectionString,
		CancellationToken cancellationToken)
	{
		var described = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(
			"""
			SELECT table_schema || '.' || table_name AS qualified, column_name
			FROM information_schema.columns
			WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
			""",
			connection);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var qualified = reader.GetString(0);
			if (!described.TryGetValue(qualified, out var columns))
			{
				columns = new HashSet<string>(StringComparer.Ordinal);
				described[qualified] = columns;
			}

			_ = columns.Add(reader.GetString(1));
		}

		return described;
	}

	private async Task<string> CreateDatabaseAsync(string databaseName)
	{
		await using var maintenance = new NpgsqlConnection(_fixture.ConnectionString);
		await maintenance.OpenAsync(CancellationToken.None).ConfigureAwait(true);

		// CA2100: the only interpolated value is a GUID "N" format -- 32 hex characters, no user input and
		// no quotable character. A database name cannot be parameterised in SQL.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", maintenance);
#pragma warning restore CA2100
		_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);

		return new NpgsqlConnectionStringBuilder(_fixture.ConnectionString) { Database = databaseName }
			.ConnectionString;
	}

	private async Task DropDatabaseAsync(string databaseName, string connectionString)
	{
		if (!_fixture.DockerAvailable || connectionString.Length == 0)
		{
			return;
		}

		// Scoped to this suite's own connection strings rather than ClearAllPools(): the pool is keyed by
		// connection string, so clearing globally would evict connections of suites running alongside.
		using (var pooled = new NpgsqlConnection(connectionString))
		{
			NpgsqlConnection.ClearPool(pooled);
		}

		await using var maintenance = new NpgsqlConnection(_fixture.ConnectionString);
		await maintenance.OpenAsync(CancellationToken.None).ConfigureAwait(true);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities -- GUID-derived name
		await using var drop = new NpgsqlCommand(
			$"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", maintenance);
#pragma warning restore CA2100
		_ = await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
	}

	/// <summary>
	/// The store a consumer gets when they opt into auto-creation, with every table name left shipped.
	/// </summary>
	/// <remarks>
	/// NO TABLE NAME IS OVERRIDDEN, deliberately. Sibling suites suffix names per arm so one arm's
	/// deliberately-wrong table cannot break the others; here that would compare a table the shipped
	/// scripts never create against nothing, and the intersection would be empty.
	/// </remarks>
	private PostgresDataInventoryStore CreateAutoCreatingStore() => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresDataInventoryStoreOptions
		{
			ConnectionString = _autoCreatedConnectionString,
			AutoCreateSchema = true,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<PostgresDataInventoryStore>(),
		TenantContextHolder.AmbientContext,
		tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new TenantContextOptions()));
}
