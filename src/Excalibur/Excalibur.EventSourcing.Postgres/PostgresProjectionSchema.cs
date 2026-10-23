// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
/// <b>There is no schema parameter, deliberately.</b> The store addresses the table unqualified, so
/// it resolves through <c>search_path</c>. A script that created the table in a named schema would
/// produce a table the store cannot find unless the connection's <c>search_path</c> happened to
/// include it — a knob the consuming code cannot honour is worse than no knob. Set
/// <c>search_path</c> on the connection if the table does not belong in the default schema.
/// </para>
/// <para>
/// <b>It does not create the table for you.</b> A store that silently created a missing table would
/// also create it in the wrong database when a connection string is wrong, and a projection reading
/// an empty table it just made is indistinguishable from one that is merely behind.
/// </para>
/// </remarks>
public static class PostgresProjectionSchema
{
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
	/// <returns>A script that creates the table if it does not already exist.</returns>
	/// <exception cref="ArgumentException">
	/// The name is empty, whitespace, or contains a character that is not a letter, a digit or an
	/// underscore. The identifier is interpolated into the script, so it is restricted rather than
	/// quoted-and-hoped: a table name is a deployment-time constant, never user input.
	/// </exception>
	public static string CreateTableScript(string tableName)
	{
		var table = RequireIdentifier(tableName);

		return string.Format(
			CultureInfo.InvariantCulture,
			"""
			CREATE TABLE IF NOT EXISTS "{0}" (
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
			Excalibur.Dispatch.TenantId.MaxLength.ToString(CultureInfo.InvariantCulture));
	}

	// Interpolated, so restricted. See the exception remark on CreateTableScript.
	private static string RequireIdentifier(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		foreach (var character in value)
		{
			if (!char.IsLetterOrDigit(character) && character != '_')
			{
				throw new ArgumentException(
					$"'{value}' is not a usable Postgres identifier here: only letters, digits and "
					+ "underscores are accepted, because the name is interpolated into the generated "
					+ "script. Rename the table, or create it with your own migration tooling.",
					nameof(value));
			}
		}

		return value;
	}
}
