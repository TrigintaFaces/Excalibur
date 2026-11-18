// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Text;

using Excalibur.Data;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Excalibur.Outbox.Oracle;

/// <summary>One operation's resolved local physical targets and exact durable authority identity.</summary>
internal sealed record OraclePhysicalOutboxScope(string OutboxTable, string FenceTable, string ArchiveTable, string Key)
{
	internal static string Exact(string left, string right) =>
		$"(UTL_I18N.STRING_TO_RAW({left}, 'AL32UTF8') IS NOT NULL AND UTL_I18N.STRING_TO_RAW({right}, 'AL32UTF8') IS NOT NULL " +
		$"AND UTL_RAW.LENGTH(UTL_I18N.STRING_TO_RAW({left}, 'AL32UTF8')) = UTL_RAW.LENGTH(UTL_I18N.STRING_TO_RAW({right}, 'AL32UTF8')) " +
		$"AND UTL_I18N.STRING_TO_RAW({left}, 'AL32UTF8') = UTL_I18N.STRING_TO_RAW({right}, 'AL32UTF8'))";

	internal static string Encode(string owner, string table)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(owner);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);
		var result = "oracle-table-v1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(owner)) + ":" +
			Convert.ToBase64String(Encoding.UTF8.GetBytes(table));
		if (Encoding.ASCII.GetByteCount(result) > 600)
		{
			throw new InvalidOperationException("Resolved Oracle outbox identity exceeds the 600-byte fencing scope capacity.");
		}
		return result;
	}

	internal static async Task<OraclePhysicalOutboxScope> ResolveAsync(OracleConnection connection,
		DbTransaction transaction, OracleOutboxStoreOptions options, CancellationToken token)
	{
		var outbox = await ResolveTableAsync(connection, transaction, options.QualifiedOutboxTableName, token).ConfigureAwait(false);
		var fence = await ResolveTableAsync(connection, transaction, options.QualifiedFenceTableName, token).ConfigureAwait(false);
		var archive = await ResolveTableAsync(connection, transaction, options.QualifiedDeadLetterTableName, token).ConfigureAwait(false);
		await ValidateFenceKeyAsync(connection, transaction, fence, token).ConfigureAwait(false);
		return new(outbox.Sql, fence.Sql, archive.Sql, Encode(outbox.Owner, outbox.Name));
	}

	private sealed record TableIdentity(string Owner, string Name)
	{
		internal string Sql => Quote(Owner) + "." + Quote(Name);
		private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
	}

	private static OracleCommand Command(OracleConnection connection, DbTransaction transaction, string sql)
	{
		var command = connection.CreateCommand();
		command.BindByName = true;
		command.Transaction = (OracleTransaction)transaction;
		command.CommandTimeout = DbTimeouts.RegularTimeoutSeconds;
// Every call site is a private fixed SQL template; runtime values are Oracle parameters.
#pragma warning disable CA2100 // The private helper accepts only its own closed templates; no configured value is SQL text.
		command.CommandText = sql;
#pragma warning restore CA2100
		return command;
	}

	private static async Task<TableIdentity> ResolveTableAsync(OracleConnection connection, DbTransaction transaction,
		string configured, CancellationToken token)
	{
		await using var command = Command(connection, transaction, """
			DECLARE
			    v_kind NUMBER;
			BEGIN
			    DBMS_UTILITY.NAME_RESOLVE(:Configured, 0, :OwnerName, :TableName, :Part2, :DbLink, v_kind, :ObjectNumber);
			END;
			""");
		command.Parameters.Add("Configured", OracleDbType.Varchar2, configured, ParameterDirection.Input);
		foreach (var name in new[] { "OwnerName", "TableName", "Part2", "DbLink" })
		{
			command.Parameters.Add(name, OracleDbType.Varchar2, 32767).Direction = ParameterDirection.Output;
		}
		command.Parameters.Add("ObjectNumber", OracleDbType.Decimal).Direction = ParameterDirection.Output;
		await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
		var owner = Text(command.Parameters["OwnerName"].Value);
		var table = Text(command.Parameters["TableName"].Value);
		var number = command.Parameters["ObjectNumber"].Value;
		if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(table)
			|| Text(command.Parameters["Part2"].Value) is not null || Text(command.Parameters["DbLink"].Value) is not null
			|| number is not OracleDecimal { IsNull: false } objectNumber || objectNumber.Value <= 0)
		{
			throw new InvalidOperationException("Oracle outbox targets must resolve to accessible local physical base tables; remote or unresolved targets are unsupported.");
		}
		// Validate by numeric identity, then compare returned names and kind ordinally in managed code.
		// NAME_RESOLVE's part1_type is not guessed to be a particular undocumented table constant.
		await using var metadata = Command(connection, transaction,
			"SELECT owner, object_name, object_type FROM all_objects WHERE object_id=:ObjectNumber AND subobject_name IS NULL");
		metadata.Parameters.Add("ObjectNumber", OracleDbType.Decimal, objectNumber.Value, ParameterDirection.Input);
		await using var reader = await metadata.ExecuteReaderAsync(token).ConfigureAwait(false);
		if (!await reader.ReadAsync(token).ConfigureAwait(false)
			|| !string.Equals(reader.GetString(0), owner, StringComparison.Ordinal)
			|| !string.Equals(reader.GetString(1), table, StringComparison.Ordinal)
			|| !string.Equals(reader.GetString(2), "TABLE", StringComparison.Ordinal)
			|| await reader.ReadAsync(token).ConfigureAwait(false))
		{
			throw new InvalidOperationException("Oracle outbox name resolution did not identify one consistent local physical base table.");
		}
		return new(owner, table);
	}

	private static string? Text(object? value) => value switch
	{
		null or DBNull => null,
		OracleString { IsNull: true } => null,
		OracleString text => text.Value,
		string text => text.Length == 0 ? null : text,
		_ => throw new InvalidDataException("Oracle returned an unsupported name-resolution output representation."),
	};

	private static async Task ValidateFenceKeyAsync(OracleConnection connection, DbTransaction transaction,
		TableIdentity fence, CancellationToken token)
	{
		// Pseudo-collations below use BINARY for persistent PK/unique constraints (Oracle19 SQLRF).
		// Queries still need explicit RAW comparisons: USING_NLS_COMP is session-dependent in predicates.
		await using var command = Command(connection, transaction, $"""
			SELECT c.collation, c.data_type, c.data_length,
			       (SELECT COUNT(*) FROM all_constraints k
			        WHERE {Exact("k.owner", "c.owner")} AND {Exact("k.table_name", "c.table_name")}
			          AND k.constraint_type IN ('P','U') AND k.status='ENABLED' AND k.validated='VALIDATED'
			          AND (SELECT COUNT(*) FROM all_cons_columns p
			               WHERE {Exact("p.owner", "k.owner")} AND {Exact("p.constraint_name", "k.constraint_name")})=1
			          AND EXISTS (SELECT 1 FROM all_cons_columns p
			                      WHERE {Exact("p.owner", "k.owner")} AND {Exact("p.constraint_name", "k.constraint_name")}
			                        AND {Exact("p.column_name", "'SCOPE_KEY'")}))
			FROM all_tab_columns c
			WHERE {Exact("c.owner", ":OwnerName")} AND {Exact("c.table_name", ":TableName")}
			  AND {Exact("c.column_name", "'SCOPE_KEY'")}
			""");
		command.Parameters.Add("OwnerName", OracleDbType.Varchar2, fence.Owner, ParameterDirection.Input);
		command.Parameters.Add("TableName", OracleDbType.Varchar2, fence.Name, ParameterDirection.Input);
		await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
		if (!await reader.ReadAsync(token).ConfigureAwait(false))
		{
			throw new InvalidOperationException("Oracle fence table requires a visible SCOPE_KEY column with a binary single-column unique constraint.");
		}
		var collation = await reader.IsDBNullAsync(0, token).ConfigureAwait(false) ? null : reader.GetString(0);
		if (collation is not ("BINARY" or "USING_NLS_COMP" or "USING_NLS_SORT" or "USING_NLS_SORT_CS")
			|| !string.Equals(reader.GetString(1), "VARCHAR2", StringComparison.Ordinal)
			|| reader.GetDecimal(2) < 600 || reader.GetDecimal(3) < 1)
		{
			throw new InvalidOperationException("Oracle fence SCOPE_KEY requires VARCHAR2 capacity of at least 600 bytes and an enabled validated binary single-column unique key; linguistic key collations are unsupported.");
		}
	}
}
