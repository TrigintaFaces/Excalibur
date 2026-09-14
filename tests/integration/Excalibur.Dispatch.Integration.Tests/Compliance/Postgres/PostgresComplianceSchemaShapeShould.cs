// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Binds the mhr0kh fix: a self-provisioning compliance store's startup check must distinguish a
/// correctly-shaped table from a wrong-shaped one of the same name -- an existence check alone passes on
/// exactly the input it exists to reject.
/// </summary>
/// <remarks>
/// <para>
/// Each arm auto-creates a fresh table (proving the shipped shape is correct), drops one column the
/// store's own statements bind (simulating a consumer's hand-provisioned, stale-copied schema), then
/// constructs a NEW store instance -- simulating a process restart -- with <c>AutoCreateSchema = true</c>.
/// That is deliberate: <c>CREATE TABLE IF NOT EXISTS</c> only creates tables that are absent, so the
/// auto-create path is the one a wrong-shaped table survives unless the shape check also runs there.
/// Before this fix, two of these three stores' shape check ran ONLY on the <c>AutoCreateSchema = false</c>
/// path; this suite pins the auto-create path specifically, not the already-covered verify-disabled path.
/// </para>
/// <para>
/// Safety is paired with liveness: a store that refused every table, correctly-shaped or not, would
/// satisfy every safety arm here and be useless. Each store gets one liveness arm confirming an untouched,
/// correctly-shaped table still initializes and answers a read.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresComplianceSchemaShapeShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresComplianceSchemaShapeShould(PostgresFixture fixture) => _fixture = fixture;

	// ---------- PostgresDataInventoryStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenDataInventoryRegistrationsTableIsMissingAColumn()
	{
		var (registrations, discovered) = InventoryTableNames();
		using (var provisioner = CreateInventoryStore(registrations, discovered, autoCreate: true))
		{
			_ = await provisioner.GetAllRegistrationsAsync(TestCancellationToken);
		}

		await DropColumnAsync(registrations, "description");

		using var restarted = CreateInventoryStore(registrations, discovered, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetAllRegistrationsAsync(TestCancellationToken));

		thrown.Message.Contains("description", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenDataInventoryTablesAreCorrectlyShaped()
	{
		var (registrations, discovered) = InventoryTableNames();
		using var store = CreateInventoryStore(registrations, discovered, autoCreate: true);

		var result = await store.GetAllRegistrationsAsync(TestCancellationToken);

		result.ShouldNotBeNull();
	}

	// ---------- PostgresErasureStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenErasureRequestsTableIsMissingAColumn()
	{
		var (requests, certificates) = ErasureTableNames();
		using (var provisioner = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await DropColumnAsync(requests, "error_message");

		using var restarted = CreateErasureStore(requests, certificates, autoCreate: true);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

		thrown.Message.Contains("error_message", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenErasureTablesAreCorrectlyShaped()
	{
		var (requests, certificates) = ErasureTableNames();
		using var store = CreateErasureStore(requests, certificates, autoCreate: true);

		var status = await store.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);

		status.ShouldBeNull("no request was ever saved under this id; the READ must succeed with no result.");
	}

	// ---------- PostgresLegalHoldStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenLegalHoldTableIsMissingAColumn()
	{
		var table = LegalHoldTableName();
		using (var provisioner = CreateLegalHoldStore(table, autoCreate: true))
		{
			_ = await provisioner.GetHoldAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await DropColumnAsync(table, "case_reference");

		using var restarted = CreateLegalHoldStore(table, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetHoldAsync(Guid.NewGuid(), TestCancellationToken));

		thrown.Message.Contains("case_reference", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenLegalHoldTableIsCorrectlyShaped()
	{
		var table = LegalHoldTableName();
		using var store = CreateLegalHoldStore(table, autoCreate: true);

		var hold = await store.GetHoldAsync(Guid.NewGuid(), TestCancellationToken);

		hold.ShouldBeNull("no hold was ever saved under this id; the READ must succeed with no result.");
	}

	// ---------- helpers ----------

	// Each arm gets its own table pair/name, derived from the calling test, so arms never collide over a
	// dropped column another arm still needs present.
	//
	// Case is irrelevant to these identifiers and must stay uppercase-normalized: the store's DDL and the
	// ALTER below both interpolate this SAME string into a QUOTED identifier, and PostgreSQL preserves the
	// case of a quoted identifier exactly -- so both sides agree whatever the case, and normalizing upward
	// is the direction that round-trips safely.
	private static (string Registrations, string Discovered) InventoryTableNames(
		[CallerMemberName] string armName = "") =>
		($"inv_reg_{armName}".ToUpperInvariant(), $"inv_loc_{armName}".ToUpperInvariant());

	private static (string Requests, string Certificates) ErasureTableNames(
		[CallerMemberName] string armName = "") =>
		($"era_req_{armName}".ToUpperInvariant(), $"era_cert_{armName}".ToUpperInvariant());

	private static string LegalHoldTableName([CallerMemberName] string armName = "") =>
		$"hold_{armName}".ToUpperInvariant();

	private async Task DropColumnAsync(string tableName, string columnName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$@"ALTER TABLE ""compliance"".""{tableName}"" DROP COLUMN ""{columnName}""",
			cancellationToken: TestCancellationToken));
	}

	private PostgresDataInventoryStore CreateInventoryStore(
		string registrationsTable, string discoveredTable, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = registrationsTable,
			DiscoveredLocationsTableName = discoveredTable,
			AutoCreateSchema = autoCreate,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<PostgresDataInventoryStore>(),
		UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private PostgresErasureStore CreateErasureStore(
		string requestsTable, string certificatesTable, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = requestsTable,
			CertificatesTableName = certificatesTable,
			AutoCreateSchema = autoCreate,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<PostgresErasureStore>(),
		UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private PostgresLegalHoldStore CreateLegalHoldStore(string table, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			TableName = table,
			AutoCreateSchema = autoCreate,
		}),
		EnabledTestLogger.Create<PostgresLegalHoldStore>(),
		UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// Implements <see cref="IDataSubjectHasher"/> directly and inherits no first-party base. Hashing is
	/// not the property under test here (column shape is); a stable identity keeps this fixture simple.
	/// </summary>
	private sealed class PassThroughDataSubjectHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
