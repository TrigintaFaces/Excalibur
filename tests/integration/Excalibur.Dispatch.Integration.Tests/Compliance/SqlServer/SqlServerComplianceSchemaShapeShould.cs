// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the mhr0kh fix for <see cref="SqlServerDataInventoryStore"/>: startup schema verification must
/// distinguish a correctly-shaped table from a wrong-shaped one of the same name, on BOTH initialization
/// paths -- an existence check (or a shape check wired to only one path) passes on exactly the input it
/// exists to reject. Postgres twin: <c>PostgresComplianceSchemaShapeShould</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each safety arm auto-creates a fresh table (proving the shipped shape is correct), drops one column
/// the store's own statements bind (simulating a consumer's hand-provisioned, stale-copied schema), then
/// constructs a NEW store instance -- simulating a process restart -- with <c>AutoCreateSchema = true</c>.
/// That is deliberate: <c>CREATE TABLE IF NOT EXISTS</c>-equivalent guarding only creates tables that are
/// absent, so the auto-create path is the one a wrong-shaped table survives unless the shape check also
/// runs there.
/// </para>
/// <para>
/// Safety is paired with liveness: a store that refused every table, correctly-shaped or not, would
/// satisfy every safety arm here and be useless. Each table pair gets one liveness arm confirming an
/// untouched, correctly-shaped table still initializes and answers a read.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerComplianceSchemaShapeShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerComplianceSchemaShapeShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenDataInventoryRegistrationsTableIsMissingAColumn()
	{
		var (registrations, discovered) = InventoryTableNames();
		using (var provisioner = CreateInventoryStore(registrations, discovered, autoCreate: true))
		{
			_ = await provisioner.GetAllRegistrationsAsync(TestCancellationToken);
		}

		await DropColumnAsync(registrations, "Description");

		using var restarted = CreateInventoryStore(registrations, discovered, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetAllRegistrationsAsync(TestCancellationToken));

		thrown.Message.Contains("Description", StringComparison.Ordinal).ShouldBeTrue(
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

	// ---------- helpers ----------

	// Each arm gets its own table pair, derived from the calling test, so arms never collide over a
	// dropped column another arm still needs present.
	private static (string Registrations, string Discovered) InventoryTableNames(
		[CallerMemberName] string armName = "") =>
		($"InvReg{armName}", $"InvLoc{armName}");

	private async Task DropColumnAsync(string tableName, string columnName)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$"ALTER TABLE [compliance].[{tableName}] DROP COLUMN [{columnName}]",
			cancellationToken: TestCancellationToken));
	}

	private SqlServerDataInventoryStore CreateInventoryStore(
		string registrationsTable, string discoveredTable, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = registrationsTable,
			DiscoveredLocationsTableName = discoveredTable,
			AutoCreateSchema = autoCreate,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<SqlServerDataInventoryStore>(),
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
