// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Provisions, regresses, and migrates the compliance schema using the DDL the package actually SHIPS.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here restates a <c>CREATE TABLE</c>. Both the fresh-install shape and the migration are read
/// out of the package's own script files, so a suite built on this type cannot pass against a schema no
/// consumer will ever provision — the failure mode a hand-written fixture DDL produces when it drifts
/// <em>ahead</em> of the shipped file, which is worse than drifting behind because it is silent.
/// </para>
/// <para>
/// <see cref="RegressToLegacyAsync"/> is the one that needs justifying. A migration can only be tested
/// against the shape it migrates FROM, and that shape no longer exists in any file — 001 now creates the
/// total column directly. Rather than reintroduce the old definition as a copy (which would then be the
/// only place the legacy shape is written down, free to drift from what consumers actually have), it is
/// DERIVED from the shipped one by reversing exactly the three properties the migration establishes:
/// the default, the nullability, and the collation. If 001 changes, this reversal keeps describing the
/// real "before", because it is expressed relative to the real "after".
/// </para>
/// </remarks>
internal static class ShippedComplianceSchema
{
	private const string CreateScript = "SqlServer.001_CreateComplianceSchema.sql";
	private const string MigrateScript = "SqlServer.003_MakeComplianceTenantTotal.sql";
	private const string MigrateDataInventoryScript = "SqlServer.004_MakeDataInventoryTenantTotal.sql";
	private const string MigrateInventoryKeyWidthsScript = "SqlServer.006_MakeInventoryKeysFitTheIndexLimit.sql";

	/// <summary>Runs the shipped data-inventory tenant-totality migration.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task MigrateDataInventoryAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteScriptAsync(connectionString, LoadShipped(MigrateDataInventoryScript), cancellationToken);

	/// <summary>
	/// Runs the shipped index-width repair, the step that follows the tenant migration in the real upgrade
	/// chain and produces the shape a fresh install now gets from 001.
	/// </summary>
	/// <remarks>
	/// Exposed so a suite that regressed these tables can put them BACK. The inventory tables are shared by
	/// every arm in the SQL Server collection, and the legacy shape is one no current store will accept —
	/// it fails fast on the missing tenant column, by design. A suite that leaves it behind therefore does
	/// not fail alone; it fails every suite that runs after it, with an error about the schema rather than
	/// about the culprit.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task MigrateInventoryKeyWidthsAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteScriptAsync(connectionString, LoadShipped(MigrateInventoryKeyWidthsScript), cancellationToken);

	/// <summary>
	/// Returns the two inventory tables to the pre-migration shape every upgrading consumer holds: no
	/// TenantId column at all, and the narrow primary keys that let one tenant's registration overwrite
	/// another's.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the reversal that matters most, and the shape it recreates is not hypothetical. The tenant
	/// discriminator reached these tables by editing 001 in place, and BOTH provisioning paths — the
	/// script's <c>IF NOT EXISTS</c> and the store's own auto-create — guard on table existence. So every
	/// database whose inventory tables predate that edit still has exactly this shape, and upgrading the
	/// package does not change it.
	/// </para>
	/// <para>
	/// Derived from the shipped definition by reversing every property the two shipped changes establish —
	/// the tenant column and its default, the key composition, and the surrogate keys and hashed natural
	/// key the index-width repair introduced — rather than restating the legacy DDL as a copy. A copy
	/// would become the only place the old shape is written down and would be free to drift from what
	/// consumers actually have.
	/// </para>
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task RegressDataInventoryToLegacyAsync(
		string connectionString,
		CancellationToken cancellationToken)
	{
		// Rows are cleared first, and each key is only added when the table has none. Both matter, and both
		// were learned by running this: arms share one container and these table names, so a second arm
		// arrives at a table an earlier one already regressed. Without the DELETE, dropping the tenant
		// column collapses two tenants' rows into a duplicate under the narrow key and the ADD fails with
		// "duplicate key was found"; without the guard, a table that is already narrow gets a second
		// primary key. A regress that is not idempotent is a fixture that only works when it runs first.

		// The reversal spans TWO shipped changes, not one, because 001 now creates the shape BOTH of them
		// leave behind: the tenant discriminator (004) and the index-width repair (006) that moved each
		// natural key off the clustered index onto a surrogate. A legacy database predates both, so it has
		// no TenantId, no surrogate identity column, no UNIQUE natural key, and no NaturalKeyHash. Reversing
		// only the tenant half is what made this throw: the natural key's UNIQUE constraint still named
		// TenantId, so SQL Server refused to drop the column out from under it.
		//
		// Order is dependency-first and is load-bearing. The UNIQUE constraints go before the columns they
		// name; NaturalKeyHash goes before TenantId because it is a PERSISTED computed column over it; and
		// each surrogate goes after the primary key it carries.
		const string Sql = """
			DELETE FROM [compliance].[DataInventoryRegistrations];
			DELETE FROM [compliance].[DiscoveredDataLocations];

			IF EXISTS (SELECT * FROM sys.key_constraints
			           WHERE name = N'UQ_DataInventoryRegistrations_Key'
			             AND parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]'))
			    ALTER TABLE [compliance].[DataInventoryRegistrations] DROP CONSTRAINT [UQ_DataInventoryRegistrations_Key];

			IF EXISTS (SELECT * FROM sys.key_constraints
			           WHERE name = N'PK_DataInventoryRegistrations'
			             AND parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]'))
			    ALTER TABLE [compliance].[DataInventoryRegistrations] DROP CONSTRAINT [PK_DataInventoryRegistrations];

			IF EXISTS (SELECT * FROM sys.default_constraints
			           WHERE parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
			             AND name = N'DF_DataInventoryRegistrations_TenantId')
			    ALTER TABLE [compliance].[DataInventoryRegistrations] DROP CONSTRAINT [DF_DataInventoryRegistrations_TenantId];

			IF EXISTS (SELECT * FROM sys.columns
			           WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]') AND name = N'TenantId')
			    ALTER TABLE [compliance].[DataInventoryRegistrations] DROP COLUMN [TenantId];

			IF EXISTS (SELECT * FROM sys.columns
			           WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]') AND name = N'RegistrationId')
			    ALTER TABLE [compliance].[DataInventoryRegistrations] DROP COLUMN [RegistrationId];

			IF NOT EXISTS (SELECT * FROM sys.key_constraints
			               WHERE parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
			                 AND type = 'PK')
			    ALTER TABLE [compliance].[DataInventoryRegistrations]
			        ADD CONSTRAINT [PK_DataInventoryRegistrations] PRIMARY KEY ([TableName], [FieldName]);

			IF EXISTS (SELECT * FROM sys.key_constraints
			           WHERE name = N'UQ_DiscoveredDataLocations_Key'
			             AND parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]'))
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP CONSTRAINT [UQ_DiscoveredDataLocations_Key];

			IF EXISTS (SELECT * FROM sys.key_constraints
			           WHERE name = N'PK_DiscoveredDataLocations'
			             AND parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]'))
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP CONSTRAINT [PK_DiscoveredDataLocations];

			IF EXISTS (SELECT * FROM sys.columns
			           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]') AND name = N'NaturalKeyHash')
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP COLUMN [NaturalKeyHash];

			IF EXISTS (SELECT * FROM sys.default_constraints
			           WHERE parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
			             AND name = N'DF_DiscoveredDataLocations_TenantId')
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP CONSTRAINT [DF_DiscoveredDataLocations_TenantId];

			IF EXISTS (SELECT * FROM sys.columns
			           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]') AND name = N'TenantId')
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP COLUMN [TenantId];

			IF EXISTS (SELECT * FROM sys.columns
			           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]') AND name = N'LocationId')
			    ALTER TABLE [compliance].[DiscoveredDataLocations] DROP COLUMN [LocationId];

			IF NOT EXISTS (SELECT * FROM sys.key_constraints
			               WHERE parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
			                 AND type = 'PK')
			    ALTER TABLE [compliance].[DiscoveredDataLocations]
			        ADD CONSTRAINT [PK_DiscoveredDataLocations]
			            PRIMARY KEY ([DataSubjectIdHash], [TableName], [FieldName], [RecordId]);
			""";

		await ExecuteScriptAsync(connectionString, Sql, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Creates the compliance schema in its shipped, fresh-install shape.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteScriptAsync(connectionString, LoadShipped(CreateScript), cancellationToken);

	/// <summary>Runs the shipped tenant-totality migration.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task MigrateAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteScriptAsync(connectionString, LoadShipped(MigrateScript), cancellationToken);

	/// <summary>
	/// Returns the two tenant columns to the pre-migration shape a real upgrading consumer holds: nullable,
	/// no default, and no explicit collation (so the column inherits the database default, which is what
	/// 001 produced before it named a collation).
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task RegressToLegacyAsync(string connectionString, CancellationToken cancellationToken)
	{
		const string Sql = """
			IF EXISTS (SELECT * FROM sys.default_constraints
			           WHERE parent_object_id = OBJECT_ID(N'[compliance].[LegalHolds]')
			             AND name = N'DF_LegalHolds_TenantId')
			    ALTER TABLE [compliance].[LegalHolds] DROP CONSTRAINT [DF_LegalHolds_TenantId];

			IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_LegalHolds_TenantId'
			           AND object_id = OBJECT_ID(N'[compliance].[LegalHolds]'))
			    DROP INDEX [IX_LegalHolds_TenantId] ON [compliance].[LegalHolds];

			ALTER TABLE [compliance].[LegalHolds] ALTER COLUMN [TenantId] NVARCHAR(256) NULL;

			CREATE NONCLUSTERED INDEX [IX_LegalHolds_TenantId]
			    ON [compliance].[LegalHolds] ([TenantId], [IsActive]);

			IF EXISTS (SELECT * FROM sys.default_constraints
			           WHERE parent_object_id = OBJECT_ID(N'[compliance].[ErasureRequests]')
			             AND name = N'DF_ErasureRequests_TenantId')
			    ALTER TABLE [compliance].[ErasureRequests] DROP CONSTRAINT [DF_ErasureRequests_TenantId];

			IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ErasureRequests_TenantId'
			           AND object_id = OBJECT_ID(N'[compliance].[ErasureRequests]'))
			    DROP INDEX [IX_ErasureRequests_TenantId] ON [compliance].[ErasureRequests];

			ALTER TABLE [compliance].[ErasureRequests] ALTER COLUMN [TenantId] NVARCHAR(256) NULL;

			CREATE NONCLUSTERED INDEX [IX_ErasureRequests_TenantId]
			    ON [compliance].[ErasureRequests] ([TenantId], [RequestedAt]);
			""";

		await ExecuteScriptAsync(connectionString, Sql, cancellationToken).ConfigureAwait(false);
	}

	private static async Task ExecuteScriptAsync(
		string connectionString,
		string script,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var batch in SplitBatches(script))
		{
			// CA2100: the command text is the package's own DDL, embedded at compile time, plus the
			// fixed reversal above. Neither is reachable from user input, and object definitions cannot
			// be parameterised in T-SQL. Scoped so a genuine concatenation added later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static IEnumerable<string> SplitBatches(string script) =>
		script
			.Split(["\nGO\r\n", "\nGO\n", "\r\nGO\r\n", "\nGO"], StringSplitOptions.None)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0 && !batch.Equals("GO", StringComparison.OrdinalIgnoreCase));

	private static string LoadShipped(string scriptSuffix)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix rather than a hardcoded manifest name: the resource name is derived from the
		// link path, so pinning the full name would turn an unrelated restructure into a null stream
		// instead of a sentence. The suffix carries the DIALECT folder because both engines ship a file
		// named 001_CreateComplianceSchema.sql and a bare leaf would match either.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(scriptSuffix, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{scriptSuffix}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, " +
				"these suites would silently fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
