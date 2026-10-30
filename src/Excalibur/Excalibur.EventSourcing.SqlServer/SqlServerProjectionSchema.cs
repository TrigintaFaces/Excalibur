// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// ddl-migration: provisioning-only {1} - Earlier alpha projection tables require fresh provisioning and replay; this creator does not upgrade their key widths or tenant collation.

using System.Globalization;

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// The table a SQL Server projection store reads and writes, as a script you can run.
/// </summary>
/// <remarks>
/// <para>
/// <b>A projection table's columns are dictated by the store, not by the projection type.</b> The
/// projection body is stored as JSON in a single <c>Data</c> column, so a table whose columns are
/// named after the projection's own properties compiles, deploys, and then fails at the first apply
/// with <c>Invalid column name 'Data'</c> — on the consumer's machine, against their database, where
/// nothing in this framework can see it.
/// </para>
/// <para>
/// The table name is a per-projection option, so this cannot be a fixed script the way the event
/// store's schema is. It is emitted instead, from the same constants the store itself uses, which is
/// what keeps the two from drifting: change the store's columns without changing this and the
/// round-trip conformance arm fails.
/// </para>
/// <para>
/// <b>The key widths are a BUDGET, not a preference.</b> SQL Server caps a clustered index key at 900
/// bytes, and a primary key is clustered unless told otherwise. <c>NVARCHAR</c> costs two bytes per
/// character, so the tenant and identifier columns together may span at most 450 characters. The tenant
/// width derives from <c>TenantId.MaxLength</c>; the identifier allows 350 characters. Earlier widths of
/// 200 and 450 summed to 1,300 bytes — the table still CREATEd, with
/// a warning, and then refused any insert whose actual key exceeded the limit. A schema that deploys
/// cleanly and rejects rows later is worse than one that refuses up front, because the failure arrives
/// on the consumer's first long identifier rather than at deployment time.
/// </para>
/// <para>
/// <b>Those widths are therefore the portable bound on a projection identifier</b>, not a SQL Server
/// quirk to route around. Postgres permits more and its emitted schema is wider, but an identifier that
/// fits only there fails when the same projection is deployed on SQL Server.
/// </para>
/// <para>
/// <b>It does not create the table for you, and that is deliberate.</b> A store that silently created
/// a missing table would also silently create it in the wrong database when a connection string is
/// wrong, and a projection reading an empty table it just made is indistinguishable from one that is
/// merely behind. Run this where you run your other migrations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // at deployment time, once per projection table
/// var ddl = SqlServerProjectionSchema.CreateTableScript("OrderSummary");
/// await using var command = connection.CreateCommand();
/// command.CommandText = ddl;
/// await command.ExecuteNonQueryAsync(cancellationToken);
/// </code>
/// </example>
public static class SqlServerProjectionSchema
{
    /// <summary>Creates a dedicated aggregate-local transactional projection table.</summary>
    /// <param name="tableName">Physical projection table, including its generation suffix if configured.</param>
    /// <param name="schema">SQL schema.</param>
    /// <returns>Idempotent creation script. Existing ordinary tables are not converted.</returns>
    /// <remarks>The position constraint prevents an Inline or Async processor from adopting this destination.</remarks>
    public static string CreateTransactionalTableScript(string tableName, string schema = "dbo")
    {
        var script = CreateTableScript(tableName, schema);
        return script.Replace($"CONSTRAINT [PK_{tableName}]", $"""
            [TransactionalSource] NVARCHAR(300) COLLATE Latin1_General_BIN2 NULL,
            [TransactionalAggregateType] NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL,
            CONSTRAINT [CK_{tableName}_TransactionalPosition] CHECK ([LastAppliedPosition] IN (-1, -2, -3)),
            CONSTRAINT [PK_{tableName}]
            """, StringComparison.Ordinal);
    }

	// The column names are internal: once the script above exists, a consumer has no reason to
	// reconstruct the table by hand, and a name that is public is a name we cannot change. Tests reach
	// them through InternalsVisibleTo.

	/// <summary>The identifier column. Holds the projection id.</summary>
	internal const string IdColumn = "Id";

	/// <summary>The tenant column. Part of the primary key, so one table serves every tenant.</summary>
	internal const string TenantIdColumn = "TenantId";

	/// <summary>The body column. Holds the projection serialized as JSON.</summary>
	internal const string DataColumn = "Data";

	/// <summary>The creation timestamp column.</summary>
	internal const string CreatedAtColumn = "CreatedAt";

	/// <summary>The last-write timestamp column.</summary>
	internal const string UpdatedAtColumn = "UpdatedAt";

	/// <summary>
	/// The position column. Records the global stream position the stored row was folded through.
	/// </summary>
	/// <remarks>
	/// <b>NOT NULL, defaulting to <see cref="ProjectionPosition.UnplaceableSentinel"/>.</b> It cannot
	/// default to zero, because zero is a legitimate stream position and a fresh row would then claim
	/// to have folded the first event already. It cannot be nullable either: the compare-and-set that
	/// orders concurrent writers compares against this value, and a NULL compares equal to nothing.
	/// </remarks>
	internal const string LastAppliedPositionColumn = "LastAppliedPosition";

	/// <summary>
	/// Builds the <c>CREATE TABLE</c> script for a projection table, idempotently.
	/// </summary>
	/// <param name="tableName">The table name, matching the store's configured table name.</param>
	/// <param name="schema">The schema to create it in.</param>
	/// <returns>A script that creates the table if it does not already exist.</returns>
	/// <exception cref="ArgumentException">
	/// A name is empty, whitespace, or contains a character that is not a letter, a digit or an
	/// underscore. Letters and digits must be ASCII, matching the store's accepted names.
	/// Identifiers are interpolated into the script, so they are restricted rather than
	/// quoted-and-hoped: a name is a deployment-time constant, never user input, and refusing the
	/// unusual cases costs nothing while leaving no room for an injected one.
	/// </exception>
	public static string CreateTableScript(string tableName, string schema = "dbo")
	{
		var table = RequireIdentifier(tableName, nameof(tableName));
		var owner = RequireIdentifier(schema, nameof(schema));

		return string.Format(
			CultureInfo.InvariantCulture,
			"""
			IF OBJECT_ID(N'[{0}].[{1}]', N'U') IS NULL
			BEGIN
			    CREATE TABLE [{0}].[{1}]
			    (
			        [{2}]  NVARCHAR({9}) COLLATE Latin1_General_BIN2   NOT NULL,
			        [{3}]  NVARCHAR(350)   NOT NULL,
			        [{4}]  NVARCHAR(MAX)   NOT NULL,
			        [{5}]  DATETIMEOFFSET  NOT NULL,
			        [{6}]  DATETIMEOFFSET  NOT NULL,
			        [{7}]  BIGINT          NOT NULL CONSTRAINT [DF_{1}_{7}] DEFAULT ({8}),
			        CONSTRAINT [PK_{1}] PRIMARY KEY ([{2}], [{3}])
			    );
			END;
			""",
			owner,
			table,
			TenantIdColumn,
			IdColumn,
			DataColumn,
			CreatedAtColumn,
			UpdatedAtColumn,
			LastAppliedPositionColumn,
			ProjectionPosition.UnplaceableSentinel.ToString(CultureInfo.InvariantCulture),
			// DERIVED, NEVER A LITERAL. The tenant column cannot usefully be wider than the bound the
			// inbound boundary enforces, and a literal here drifted from the shipped script the moment
			// one of the two was edited -- which is exactly what happened. Deriving it makes the
			// divergence inexpressible on this side; EmitTheSameDefinitionAsTheShippedScript binds the
			// script to it.
			Excalibur.Dispatch.TenantId.MaxLength.ToString(CultureInfo.InvariantCulture));
	}

	// Interpolated, so restricted. See the exception remark on CreateTableScript.
	private static string RequireIdentifier(string value, string parameterName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

		foreach (var character in value)
		{
			if (!char.IsAsciiLetterOrDigit(character) && character != '_')
			{
				throw new ArgumentException(
					$"'{value}' is not a usable SQL Server identifier here: only ASCII letters, digits and "
					+ "underscores are accepted, because the name is interpolated into the generated "
					+ "script. Rename the table, or create it with your own migration tooling.",
					parameterName);
			}
		}

		return value;
	}
}
