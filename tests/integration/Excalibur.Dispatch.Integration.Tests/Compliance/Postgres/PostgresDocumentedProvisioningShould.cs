// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Testing.Conformance;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Rehearses the provisioning path the README documents and the options default to: the shipped scripts
/// applied by hand to a virgin database, every table name left at its shipped default, and schema
/// auto-creation OFF.
/// </summary>
/// <remarks>
/// <para>
/// NOTHING EXERCISED THIS CONFIGURATION, which is how a fresh-install blocker shipped. The suites that
/// set <c>AutoCreateSchema = false</c> all provisioned FIRST with auto-create on and then restarted with
/// it off, so the store's own DDL was always what made the schema; and the shipped-schema harness ran one
/// script. A consumer following the README ran a different script set from anything under test, and the
/// startup verifier requires two tables that set did not create.
/// </para>
/// <para>
/// ITS OWN DATABASE, AND THAT IS WHAT MAKES IT NON-VACUOUS RATHER THAN TIDINESS. The arms sharing this
/// container create the shipped default <c>erasure_minted_keys</c> and <c>erasure_token_fingerprint</c>:
/// <c>PostgresErasureStoreConformanceTests</c> suffixes four table names per arm and leaves those two at
/// their defaults with auto-create ON. So on the shared database this arm passes on a sibling's residue
/// with the mint-ledger script absent, and cannot tell the documented path from a broken one. A fresh
/// database is also the configuration the claim is actually about — a consumer installing for the first
/// time.
/// </para>
/// <para>
/// THE VERIFIER IS THE ORACLE HERE, deliberately, rather than a list of tables kept in this file. A list
/// would be a second copy of the contract the verifier already states, free to drift from it — and
/// drift between two copies of a provisioning fact is the defect this arm exists to catch. So the arm
/// asserts only that the documented path yields a store that STARTS, and lets the verifier say what that
/// requires.
/// </para>
/// <para>
/// IT IS RED WHEN A SCRIPT IS MISSING FROM THE DOCUMENTED SET. The harness derives that set from the
/// embedded resources, so dropping the mint-ledger or token-fingerprint script — from the package, from
/// the test project's EmbeddedResource items, or from the README's sequence if the two are brought back
/// into line — fails this arm with the engine naming the absent table.
/// </para>
/// <para>
/// Docker is a hard requirement rather than a skip. The property under test is what a real engine says
/// about a real schema, and a skip would report success while provisioning nothing.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresDocumentedProvisioningShould : IAsyncLifetime
{
	private readonly PostgresFixture _fixture;
	private readonly string _databaseName = $"documented_provisioning_{Guid.NewGuid():N}";

	private string _databaseConnectionString = string.Empty;

	public PostgresDocumentedProvisioningShould(PostgresFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		// Never a soft skip -- see verify-against-real-infra-not-mock.md. The property under test is what
		// a real engine reports about a hand-provisioned schema, which no fake can tell us.
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		await using (var maintenance = new NpgsqlConnection(_fixture.ConnectionString))
		{
			await maintenance.OpenAsync(CancellationToken.None).ConfigureAwait(true);

			// CA2100: the only interpolated value is _databaseName, built above from a GUID "N" format --
			// 32 hex characters, no user input and no quotable character. A database name cannot be
			// parameterised in SQL.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", maintenance);
#pragma warning restore CA2100
			_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
		}

		_databaseConnectionString = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
		{
			Database = _databaseName,
		}.ConnectionString;
	}

	public async ValueTask DisposeAsync()
	{
		if (!_fixture.DockerAvailable || _databaseConnectionString.Length == 0)
		{
			return;
		}

		// Scoped to this suite's own connection string rather than ClearAllPools(): the pool is keyed by
		// connection string, so clearing globally would evict connections belonging to suites running
		// alongside this one. Postgres refuses to drop a database with a live session, hence FORCE below.
		using (var pooled = new NpgsqlConnection(_databaseConnectionString))
		{
			NpgsqlConnection.ClearPool(pooled);
		}

		await using var maintenance = new NpgsqlConnection(_fixture.ConnectionString);
		await maintenance.OpenAsync(CancellationToken.None).ConfigureAwait(true);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities -- GUID-derived name
		await using var drop = new NpgsqlCommand(
			$"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", maintenance);
#pragma warning restore CA2100
		_ = await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
	}

	/// <summary>
	/// The documented provisioning sequence produces a store that starts with verification on and
	/// auto-creation off.
	/// </summary>
	[Fact]
	public async Task StartWithVerificationOnly_AfterTheDocumentedScriptSequence()
	{
		// Exactly what the README tells a consumer to run, in order, and nothing else.
		await ShippedCompliancePostgresSchema.EnsureCreatedAsync(
			_databaseConnectionString,
			TestContext.Current.CancellationToken).ConfigureAwait(true);

		var store = CreateStoreOnShippedDefaults();

		// Any operation forces the store's initialization, and the schema verification runs there. A read
		// is the cheapest one and needs no fixture data: the assertion is that it does not THROW, not what
		// it returns, and a request id that exists nowhere is the clearest way to say so.
		await Should.NotThrowAsync(
			() => store.GetStatusAsync(Guid.NewGuid(), TestContext.Current.CancellationToken))
			.ConfigureAwait(true);
	}

	/// <summary>
	/// Builds the store the way a consumer gets it: every table name shipped, verification on.
	/// </summary>
	/// <remarks>
	/// NO TABLE NAME IS OVERRIDDEN, and that is the whole point of this fixture rather than an oversight.
	/// Its sibling suites name tables per arm so that one arm's deliberately-wrong table cannot break the
	/// others -- correct for them, and fatal here: the shipped scripts create the shipped names, so an arm
	/// that renames them tests a schema the scripts never produced and could pass with the mint ledger
	/// absent.
	/// </remarks>
	private PostgresErasureStore CreateStoreOnShippedDefaults() => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresErasureStoreOptions
		{
			ConnectionString = _databaseConnectionString,

			// The documented default, stated rather than inherited: it is the configuration under test,
			// and the option's own summary explains why it ships false -- an application connection
			// holding DDL privileges is a posture regulated environments refuse, so the script path is
			// the DEFAULT path rather than an alternative one.
			AutoCreateSchema = false,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<PostgresErasureStore>(),
		TenantContextHolder.AmbientContext,
		tenantContextOptions: Microsoft.Extensions.Options.Options.Create(
			new Excalibur.Dispatch.TenantContextOptions()));
}
