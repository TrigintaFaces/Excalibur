// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Provisions the Postgres compliance schema using the DDL the package ships, and can put the two
/// inventory tables back into the pre-tenant shape an upgrading consumer still holds.
/// </summary>
/// <remarks>
/// <para>
/// The SQL Server twin of this type carries the full rationale; the same two rules apply here. Nothing
/// restates a <c>CREATE TABLE</c>, so a suite built on this cannot pass against a schema no consumer will
/// provision. And <see cref="RegressDataInventoryToPreTenantAsync"/> DERIVES the pre-tenant shape by
/// reversing exactly the properties the create script establishes, rather than keeping a second copy of
/// the old definition that would then be free to drift from what upgrading consumers actually hold.
/// </para>
/// <para>
/// The reversal is shorter than the SQL Server one, and the difference is real rather than an oversight:
/// this dialect's keys fit its index limits directly, so there is no surrogate column and no hashed
/// natural key to undo.
/// </para>
/// </remarks>
internal static class ShippedCompliancePostgresSchema
{
	private const string CreateScript = "Postgres.001_CreateComplianceSchema.sql";

	/// <summary>Creates the compliance schema in its shipped, fresh-install shape.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteAsync(connectionString, LoadShipped(CreateScript), cancellationToken);

	/// <summary>
	/// Drops the two inventory tables and re-provisions them from the shipped create script.
	/// </summary>
	/// <remarks>
	/// The repair a suite that regressed these tables owes its neighbours. The inventory tables are shared
	/// by every arm in the Postgres collection, and the pre-tenant shape is one no current store will
	/// accept — it fails fast on the missing tenant column, by design. DROP first because the create
	/// script guards on table existence and would otherwise do nothing at all.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ReprovisionDataInventoryAsync(
		string connectionString,
		CancellationToken cancellationToken)
	{
		await ExecuteAsync(
			connectionString,
			"""
			DROP TABLE IF EXISTS "compliance"."discovered_data_locations";
			DROP TABLE IF EXISTS "compliance"."data_inventory_registrations";
			""",
			cancellationToken).ConfigureAwait(false);

		await EnsureCreatedAsync(connectionString, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Returns the two inventory tables to the pre-tenant shape an upgrading consumer holds: no tenant_id
	/// column at all, and the narrow primary keys that let one tenant's registration overwrite another's.
	/// </summary>
	/// <remarks>
	/// The shape this recreates is not hypothetical. Both provisioning paths — the script's
	/// <c>CREATE TABLE IF NOT EXISTS</c> and the store's own auto-create — guard on table existence, so
	/// every database whose inventory tables predate the tenant discriminator still has exactly this
	/// shape, and upgrading the package does not change it. That is what the store's fail-fast is for, and
	/// this is the only way to hand it that input. Dropping the column takes its primary key with it, so
	/// each key is restated in its narrow form.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RegressDataInventoryToPreTenantAsync(
		string connectionString,
		CancellationToken cancellationToken) =>
		ExecuteAsync(
			connectionString,
			// Rows are cleared first, and the key is only added when the table has none. Both matter, and
			// both were learned by running this: arms share one container and these table names, so a
			// second arm arrives at a table an earlier one already regressed. Without the DELETE, dropping
			// the tenant column collapses two tenants' rows into a duplicate under the narrow key and the
			// ADD fails; without the guard, a table that is already narrow gets a second primary key and
			// PostgreSQL rejects it with 42P16. A regress that is not idempotent is a fixture that only
			// works when it runs first.
			"""
			DELETE FROM "compliance"."data_inventory_registrations";
			DELETE FROM "compliance"."discovered_data_locations";

			ALTER TABLE "compliance"."data_inventory_registrations" DROP COLUMN IF EXISTS tenant_id;
			ALTER TABLE "compliance"."discovered_data_locations" DROP COLUMN IF EXISTS tenant_id;

			DO $$
			BEGIN
			    IF NOT EXISTS (
			        SELECT 1 FROM pg_constraint con
			        JOIN pg_class rel ON rel.oid = con.conrelid
			        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
			        WHERE nsp.nspname = 'compliance'
			          AND rel.relname = 'data_inventory_registrations'
			          AND con.contype = 'p'
			    ) THEN
			        ALTER TABLE "compliance"."data_inventory_registrations"
			            ADD PRIMARY KEY (table_name, field_name);
			    END IF;

			    IF NOT EXISTS (
			        SELECT 1 FROM pg_constraint con
			        JOIN pg_class rel ON rel.oid = con.conrelid
			        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
			        WHERE nsp.nspname = 'compliance'
			          AND rel.relname = 'discovered_data_locations'
			          AND con.contype = 'p'
			    ) THEN
			        ALTER TABLE "compliance"."discovered_data_locations"
			            ADD PRIMARY KEY (data_subject_id_hash, table_name, field_name, record_id);
			    END IF;
			END
			$$;
			""",
			cancellationToken);

	private static async Task ExecuteAsync(
		string connectionString,
		string script,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// CA2100: the command text is the package's own DDL, embedded at compile time, plus the fixed
		// reversal above. Neither is reachable from user input, and object definitions cannot be
		// parameterised. Scoped so a genuine concatenation added later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(script, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static string LoadShipped(string scriptSuffix)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix rather than a hardcoded manifest name. The suffix carries the DIALECT folder
		// because both engines ship a file named 001_CreateComplianceSchema.sql, and a bare leaf would
		// match either — this suite would then silently provision SQL Server DDL against Postgres.
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
