// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ddl-migration: provisioning-only {0} - Earlier alpha projection tables require fresh provisioning and replay; this creator does not narrow existing tenant columns.

using System.Globalization;

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// The table a Postgres projection store reads and writes, as a script you can run.
/// </summary>
/// <remarks>
/// <para>
/// <b>A projection table's columns are dictated by the store, not by the projection type.</b> The
/// projection body is stored as JSON in a single <c>data</c> column, so a table whose columns are
/// named after the projection's own properties will create, deploy, and then fail at the first apply
/// — on the consumer's database, where nothing in this framework can see it.
/// </para>
/// <para>
/// The table name is a per-projection option, so this cannot be a fixed script the way the event
/// store's schema is. It is emitted instead, from the same sentinel the store itself reads, which is
/// what keeps the two from drifting.
/// </para>
/// <para>
/// <b>The schema is a parameter, and it must match the store's.</b> Every statement the store runs
/// is qualified from its own configured schema, so a table created in a different schema is a table
/// the store cannot find — with no error until the first read. Pass the same value the store is
/// configured with; both default to <c>public</c>. Both identifiers are quoted, so their case is
/// preserved exactly as given and <c>Public</c> is not <c>public</c>.
/// </para>
/// <para>
/// <b>It does not create the table for you.</b> A store that silently created a missing table would
/// also create it in the wrong database when a connection string is wrong, and a projection reading
/// an empty table it just made is indistinguishable from one that is merely behind.
/// </para>
/// </remarks>
public static class PostgresProjectionSchema
{
    /// <summary>Creates a dedicated aggregate-local transactional projection table.</summary>
    /// <param name="tableName">Physical destination table, including its generation suffix.</param>
    /// <param name="schema">SQL schema.</param>
    /// <returns>Idempotent creation script; existing ordinary tables are not converted.</returns>
    public static string CreateTransactionalTableScript(string tableName, string schema = "public") =>
        CreateTableScript(tableName, schema).Replace("PRIMARY KEY (id, tenant_id)", """
            transactional_source VARCHAR(300) COLLATE "C" NULL,
            transactional_aggregate_type VARCHAR(500) COLLATE "C" NULL,
            CHECK (last_applied_position IN (-1, -2, -3)),
            PRIMARY KEY (id, tenant_id)
            """, StringComparison.Ordinal);

	// Unquoted in the store's SQL, so lowercase: an unquoted identifier folds to lower case in
	// Postgres, and a quoted mixed-case column would then be unreachable from those queries.
	internal const string IdColumn = "id";
	internal const string TenantIdColumn = "tenant_id";
	internal const string DataColumn = "data";
	internal const string CreatedAtColumn = "created_at";
	internal const string UpdatedAtColumn = "updated_at";
	internal const string LastAppliedPositionColumn = "last_applied_position";

	/// <summary>
	/// Builds the <c>CREATE TABLE</c> script for a projection table, idempotently.
	/// </summary>
	/// <param name="tableName">The table name, matching the store's configured table name.</param>
	/// <param name="schema">The schema to create it in, matching the store's configured schema.</param>
	/// <returns>A script that creates the table if it does not already exist.</returns>
	/// <exception cref="ArgumentException">
	/// A name is empty, whitespace, or contains a character that is not a letter, a digit or an
	/// underscore. The identifiers are interpolated into the script, so they are restricted rather than
	/// quoted-and-hoped: these names are deployment-time constants, never user input.
	/// </exception>
	public static string CreateTableScript(string tableName, string schema = "public")
	{
		var table = RequireIdentifier(tableName, nameof(tableName));
		var owner = RequireIdentifier(schema, nameof(schema));

		return string.Format(
			CultureInfo.InvariantCulture,
			"""
			CREATE TABLE IF NOT EXISTS "{9}"."{0}" (
			    {1} VARCHAR(450) NOT NULL,
			    {2} VARCHAR({8}) NOT NULL,
			    {3} JSONB NOT NULL,
			    {4} TIMESTAMPTZ NOT NULL,
			    {5} TIMESTAMPTZ NOT NULL,
			    {6} BIGINT NOT NULL DEFAULT {7},
			    PRIMARY KEY ({1}, {2})
			);
			""",
			table,
			IdColumn,
			TenantIdColumn,
			DataColumn,
			CreatedAtColumn,
			UpdatedAtColumn,
			LastAppliedPositionColumn,
			ProjectionPosition.UnplaceableSentinel.ToString(CultureInfo.InvariantCulture),
			// DERIVED, NEVER A LITERAL -- see the note on the SqlServer generator. A literal drifted from
			// the shipped script as soon as either was edited.
			Excalibur.Dispatch.TenantId.MaxLength.ToString(CultureInfo.InvariantCulture),
			owner);
	}

	// Interpolated, so restricted. See the exception remark on CreateTableScript.
	private static string RequireIdentifier(string value, string paramName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

		foreach (var character in value)
		{
			if (!char.IsLetterOrDigit(character) && character != '_')
			{
				throw new ArgumentException(
					$"'{value}' is not a usable Postgres identifier here: only letters, digits and "
					+ "underscores are accepted, because the name is interpolated into the generated "
					+ "script. Rename it, or create the table with your own migration tooling.",
					paramName);
			}
		}

		return value;
	}
}
