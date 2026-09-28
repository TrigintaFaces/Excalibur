// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Provisions the compliance schema using the DDL the package actually SHIPS, and can put the two
/// inventory tables back into the pre-tenant shape an upgrading consumer still holds.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here restates a <c>CREATE TABLE</c>. The fresh-install shape is read out of the package's own
/// script file, so a suite built on this type cannot pass against a schema no consumer will ever
/// provision — the failure mode a hand-written fixture DDL produces when it drifts <em>ahead</em> of the
/// shipped file, which is worse than drifting behind because it is silent.
/// </para>
/// <para>
/// <see cref="RegressDataInventoryToPreTenantAsync"/> is the one that needs justifying. The package ships
/// no in-place upgrade — one CREATE script per provider, already at the final shape — so the pre-tenant
/// shape exists in no file. It is the input the store's fail-fast exists to refuse, and the only way to
/// produce it is to DERIVE it from the shipped one by reversing exactly the properties the create script
/// establishes. Restating the old DDL as a copy would make this the only place it is written down, free
/// to drift from what upgrading consumers actually have; expressed as a reversal it keeps describing the
/// real "before" whenever the "after" changes.
/// </para>
/// </remarks>
internal static class ShippedComplianceSchema
{
	private const string CreateScript = "SqlServer.001_CreateComplianceSchema.sql";

	/// <summary>Creates the compliance schema in its shipped, fresh-install shape.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteScriptAsync(connectionString, LoadShipped(CreateScript), cancellationToken);

	/// <summary>
	/// Drops the two inventory tables and re-provisions them from the shipped create script.
	/// </summary>
	/// <remarks>
	/// The repair a suite that regressed these tables owes its neighbours. The inventory tables are shared
	/// by every arm in the SQL Server collection, and the pre-tenant shape is one no current store will
	/// accept — it fails fast on the missing tenant column, by design. A suite that leaves it behind
	/// therefore does not fail alone; it fails every suite that runs after it, with an error about the
	/// schema rather than about the culprit. DROP first because the create script guards on table
	/// existence and would otherwise do nothing at all.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ReprovisionDataInventoryAsync(
		string connectionString,
		CancellationToken cancellationToken)
	{
		const string Sql = """
			DROP TABLE IF EXISTS [compliance].[DiscoveredDataLocations];
			DROP TABLE IF EXISTS [compliance].[DataInventoryRegistrations];
			""";

		await ExecuteScriptAsync(connectionString, Sql, cancellationToken).ConfigureAwait(false);
		await EnsureCreatedAsync(connectionString, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Returns the two inventory tables to the pre-tenant shape an upgrading consumer holds: no TenantId
	/// column at all, and the narrow primary keys that let one tenant's registration overwrite another's.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The shape this recreates is not hypothetical. Both provisioning paths — the script's
	/// <c>IF NOT EXISTS</c> and the store's own auto-create — guard on table EXISTENCE, so every database
	/// whose inventory tables predate the tenant discriminator still has exactly this shape, and upgrading
	/// the package does not change it. That is what the store's fail-fast is for, and this is the only way
	/// to hand it that input.
	/// </para>
	/// <para>
	/// Derived from the shipped definition by reversing every property it establishes — the tenant column
	/// and its default, the key composition, and the surrogate keys and hashed natural key that keep those
	/// keys inside SQL Server's index-width limit — rather than restating the old DDL as a copy.
	/// </para>
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task RegressDataInventoryToPreTenantAsync(
		string connectionString,
		CancellationToken cancellationToken)
	{
		// Rows are cleared first, and each key is only added when the table has none. Both matter, and both
		// were learned by running this: arms share one container and these table names, so a second arm
		// arrives at a table an earlier one already regressed. Without the DELETE, dropping the tenant
		// column collapses two tenants' rows into a duplicate under the narrow key and the ADD fails with
		// "duplicate key was found"; without the guard, a table that is already narrow gets a second
		// primary key. A regress that is not idempotent is a fixture that only works when it runs first.

		// The reversal spans the whole shipped shape, not only the tenant column: a pre-tenant database has
		// no TenantId, no surrogate identity column, no UNIQUE natural key, and no NaturalKeyHash.
		// Reversing only the tenant half throws — the natural key's UNIQUE constraint still names TenantId,
		// so SQL Server refuses to drop the column out from under it.
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
