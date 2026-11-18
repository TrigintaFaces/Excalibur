// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;

using SqlServerContainerFixture = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerContainerFixture;
using SqlServerTestCollection = Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures.SqlServerTestCollection;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Rehearses the provisioning path the README documents and the options default to: the shipped scripts
/// applied by hand to a virgin database, every table name left at its shipped default, and schema
/// auto-creation OFF.
/// </summary>
/// <remarks>
/// <para>
/// NOTHING EXERCISED THIS CONFIGURATION, which is how a fresh-install blocker shipped. The suites that
/// set <c>AutoCreateSchema = false</c> all provisioned FIRST with auto-create on and then restarted with
/// it off, so the store's own DDL was always what made the schema; and <c>ShippedComplianceSchema</c> ran
/// <c>001</c> alone. A consumer following the README ran a different script set from anything under test,
/// and the startup verifier requires two tables that set did not create.
/// </para>
/// <para>
/// ITS OWN DATABASE, AND THAT IS WHAT MAKES IT NON-VACUOUS RATHER THAN TIDINESS. The arms sharing this
/// container create the shipped default <c>ErasureMintedKeys</c> and <c>ErasureTokenFingerprint</c> with
/// auto-create ON, so on the shared database this arm would pass on a sibling's residue with the
/// mint-ledger script absent and could not tell the documented path from a broken one. A fresh database
/// is also the configuration the claim is actually about — a consumer installing for the first time.
/// </para>
/// <para>
/// THE VERIFIER IS THE ORACLE HERE, deliberately, rather than a list of tables kept in this file. A list
/// would be a second copy of the contract the verifier already states, free to drift from it — and
/// drift between two copies of a provisioning fact is the defect this arm exists to catch.
/// </para>
/// <para>
/// IT IS RED WHEN A SCRIPT IS MISSING FROM THE DOCUMENTED SET, because the harness derives that set from
/// the embedded resources under <c>Schema/Compliance/SqlServer/</c>. Dropping the mint-ledger or
/// token-fingerprint script — from the package or from the test project's EmbeddedResource items — fails
/// this arm with the engine naming the absent table.
/// </para>
/// <para>
/// Docker is a hard requirement rather than a skip. The property under test is what a real engine says
/// about a real schema, and a skip would report success while provisioning nothing.
/// </para>
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerDocumentedProvisioningShould : IAsyncLifetime
{
	private readonly SqlServerContainerFixture _fixture;
	private readonly string _databaseName = $"documented_provisioning_{Guid.NewGuid():N}";

	private string _databaseConnectionString = string.Empty;

	public SqlServerDocumentedProvisioningShould(SqlServerContainerFixture fixture) => _fixture = fixture;

	public async ValueTask InitializeAsync()
	{
		// Never a soft skip -- see verify-against-real-infra-not-mock.md.
		_fixture.EnsureAvailable();

		await using var master = new SqlConnection(_fixture.ConnectionString);
		await master.OpenAsync(CancellationToken.None).ConfigureAwait(true);

		// CA2100: the only interpolated value is _databaseName, built above from a GUID "N" format --
		// 32 hex characters, no user input and no quotable character. A database name cannot be
		// parameterised in T-SQL.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var create = new SqlCommand($"CREATE DATABASE [{_databaseName}]", master);
#pragma warning restore CA2100
		_ = await create.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);

		_databaseConnectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
		{
			InitialCatalog = _databaseName,
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
		// alongside this one.
		using (var pooled = new SqlConnection(_databaseConnectionString))
		{
			SqlConnection.ClearPool(pooled);
		}

		await using var master = new SqlConnection(_fixture.ConnectionString);
		await master.OpenAsync(CancellationToken.None).ConfigureAwait(true);

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities -- GUID-derived name
		await using var drop = new SqlCommand(
			$"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
			+ $"DROP DATABASE [{_databaseName}]",
			master);
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
		// Exactly what the README tells a consumer to run, in order. Key escrow (002) is excluded for the
		// reason the README gives: it provisions a different store and is conditional on registering it.
		await ShippedComplianceSchema.EnsureCreatedAsync(
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
	/// The shipped scripts create the shipped names, so an arm that renames them tests a schema the
	/// scripts never produced and could pass with the mint ledger absent.
	/// </remarks>
	private SqlServerErasureStore CreateStoreOnShippedDefaults() => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
		{
			ConnectionString = _databaseConnectionString,

			// The documented default, stated rather than inherited: it is the configuration under test.
			AutoCreateSchema = false,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<SqlServerErasureStore>(),
		TenantContextHolder.AmbientContext,
		tenantContextOptions: Microsoft.Extensions.Options.Options.Create(
			new Excalibur.Dispatch.TenantContextOptions()));
}
