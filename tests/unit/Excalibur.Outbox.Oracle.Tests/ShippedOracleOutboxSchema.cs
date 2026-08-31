// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Provisions the Oracle outbox tables from the DDL the package ships, and runs the upgrade script
/// the package ships.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OracleOutboxStoreContainerFixture"/> hand-writes its own <c>CREATE TABLE</c>. That is
/// reasonable for the store's behavioural suites, but it means nothing in the tree exercised the
/// scripts a consumer actually runs — and for Oracle that gap is wider than for the other providers,
/// because the upgrade script contains PL/SQL. A syntax error in an anonymous block is not a
/// compile-time defect anywhere in this repository; it is discovered by running it, or by a consumer.
/// </para>
/// <para>
/// The splitter below is the reason this type exists rather than a single <c>ExecuteNonQuery</c>.
/// Oracle scripts mix two statement terminators: plain SQL ends at a <c>;</c>, while an anonymous
/// PL/SQL block contains semicolons internally and is terminated by a lone <c>/</c> on its own line.
/// A driver executes exactly one statement per command, so the script has to be split the same way
/// SQL*Plus and SQLcl split it, or the block is truncated at its first internal semicolon.
/// </para>
/// </remarks>
internal static class ShippedOracleOutboxSchema
{
	private const string CreateScriptFileName = "001_CreateOracleOutboxSchema.sql";
	private const string MigrationScriptFileName = "002_MakeOracleOutboxTenantTotal.sql";
	private const string DeadLetterMigrationScriptFileName = "003_CarryOracleDeadLetterTenant.sql";

	/// <summary>
	/// Gets the shipped fresh-install DDL.
	/// </summary>
	public static string CreateDdl { get; } = LoadShipped(CreateScriptFileName);

	/// <summary>
	/// Gets the shipped tenant-totality upgrade script.
	/// </summary>
	public static string MigrationDdl { get; } = LoadShipped(MigrationScriptFileName);

	/// <summary>
	/// Drops the outbox tables and recreates them from the shipped fresh-install script.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task CreateFreshAsync(string connectionString, CancellationToken cancellationToken)
	{
		foreach (var table in new[] { "OUTBOX", "OUTBOX_DEAD_LETTERS", "OUTBOX_FENCE" })
		{
			// ORA-00942 (table does not exist) is the expected result on a first run.
			await TryExecuteAsync(connectionString, $"DROP TABLE {table} CASCADE CONSTRAINTS", 942, cancellationToken)
				.ConfigureAwait(false);
		}

		await RunScriptAsync(connectionString, CreateDdl, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Runs the shipped tenant-totality upgrade script.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RunMigrationAsync(string connectionString, CancellationToken cancellationToken) =>
		RunScriptAsync(connectionString, MigrationDdl, cancellationToken);

	/// <summary>
	/// Gets the shipped dead-letter tenant-provenance upgrade script.
	/// </summary>
	public static string DeadLetterMigrationDdl { get; } = LoadShipped(DeadLetterMigrationScriptFileName);

	/// <summary>
	/// Runs the shipped dead-letter tenant-provenance upgrade script.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RunDeadLetterMigrationAsync(
		string connectionString, CancellationToken cancellationToken) =>
		RunScriptAsync(connectionString, DeadLetterMigrationDdl, cancellationToken);

	/// <summary>
	/// Re-opens the dead-letter table to its pre-wave shape: no tenant column, and a unique key on the
	/// message id alone.
	/// </summary>
	/// <remarks>
	/// Reconstructed from the CURRENT shipped schema rather than hand-written, for the same reason as the
	/// outbox equivalent below. The constraint is dropped before the column so the drop cannot silently
	/// take the key with it and leave the table in a shape no released version ever produced.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task RemoveDeadLetterTenantColumnToLegacyShapeAsync(
		string connectionString, CancellationToken cancellationToken)
	{
		await ExecuteAsync(
			connectionString,
			"ALTER TABLE OUTBOX_DEAD_LETTERS DROP CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID",
			cancellationToken).ConfigureAwait(false);
		await ExecuteAsync(
			connectionString,
			"ALTER TABLE OUTBOX_DEAD_LETTERS DROP COLUMN TENANT_ID",
			cancellationToken).ConfigureAwait(false);
		await ExecuteAsync(
			connectionString,
			"ALTER TABLE OUTBOX_DEAD_LETTERS ADD CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID UNIQUE (MESSAGE_ID)",
			cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Re-opens the tenant column to its pre-wave shape: nullable, with no default.
	/// </summary>
	/// <remarks>
	/// Reconstructed from the CURRENT shipped schema rather than hand-written, so it cannot drift from
	/// the product: it creates today's table and re-opens the one column, which is exactly the state a
	/// database created before this wave is in.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ReopenTenantColumnToLegacyShapeAsync(
		string connectionString, CancellationToken cancellationToken)
	{
		await ExecuteAsync(connectionString, "ALTER TABLE OUTBOX MODIFY (TENANT_ID DEFAULT NULL)", cancellationToken)
			.ConfigureAwait(false);
		await ExecuteAsync(connectionString, "ALTER TABLE OUTBOX MODIFY (TENANT_ID NULL)", cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task RunScriptAsync(
		string connectionString, string script, CancellationToken cancellationToken)
	{
		foreach (var statement in SplitStatements(script))
		{
			// ORA-00955: the fresh-install script is a bare CREATE, so re-entry finds the object present.
			// That is the documented, expected behaviour of running 001 twice.
			await TryExecuteAsync(connectionString, statement, 955, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Splits an Oracle script the way SQL*Plus does: a lone <c>/</c> terminates a PL/SQL block, a
	/// <c>;</c> terminates a plain statement.
	/// </summary>
	/// <remarks>
	/// Comment-only and blank lines are dropped so an all-comment trailing section does not become an
	/// empty statement. A buffer that has begun a <c>DECLARE</c> or <c>BEGIN</c> ignores semicolons
	/// entirely and waits for its <c>/</c> — that is the whole point, since such a block is full of
	/// them.
	/// </remarks>
	internal static IEnumerable<string> SplitStatements(string script)
	{
		var buffer = new StringBuilder();
		var inPlSqlBlock = false;

		foreach (var rawLine in script.Split('\n'))
		{
			var line = rawLine.TrimEnd('\r');
			var trimmed = line.Trim();

			if (buffer.Length == 0 && (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal)))
			{
				continue;
			}

			// WHENEVER SQLERROR / OSERROR are SQL*Plus CLIENT directives, not statements. The shipped
			// scripts carry them so an unattended sqlplus run exits non-zero on a refusal instead of
			// reporting a declined migration as applied. A driver has no such notion and rejects the
			// line outright with ORA-00900, so this reader -- which feeds the script to ODP.NET rather
			// than to sqlplus -- has to drop them, exactly as the shared ShippedSchemaScript helper does
			// for the suites that use it. Two readers, one script: both must know.
			if (buffer.Length == 0
				&& trimmed.StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase)
				&& (trimmed.Contains("SQLERROR", StringComparison.OrdinalIgnoreCase)
					|| trimmed.Contains("OSERROR", StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			if (trimmed == "/")
			{
				var block = buffer.ToString().Trim();
				buffer.Clear();
				inPlSqlBlock = false;
				if (block.Length > 0)
				{
					yield return block;
				}

				continue;
			}

			if (buffer.Length == 0
				&& (trimmed.StartsWith("DECLARE", StringComparison.OrdinalIgnoreCase)
					|| trimmed.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase)))
			{
				inPlSqlBlock = true;
			}

			_ = buffer.Append(line).Append('\n');

			if (!inPlSqlBlock && trimmed.EndsWith(';'))
			{
				var statement = buffer.ToString().Trim().TrimEnd(';').Trim();
				buffer.Clear();
				if (statement.Length > 0)
				{
					yield return statement;
				}
			}
		}

		var tail = buffer.ToString().Trim().TrimEnd(';').Trim();
		if (tail.Length > 0)
		{
			yield return tail;
		}
	}

	private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
	{
		await using var connection = new OracleConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// CA2100: the command text is the package's own DDL, embedded at compile time, plus fixed
		// literals in this file. It is not reachable from user input and object definitions cannot be
		// parameterised.
#pragma warning disable CA2100
		await using var command = new OracleCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task TryExecuteAsync(
		string connectionString, string sql, int ignoredOracleErrorNumber, CancellationToken cancellationToken)
	{
		try
		{
			await ExecuteAsync(connectionString, sql, cancellationToken).ConfigureAwait(false);
		}
		catch (OracleException ex) when (ex.Number == ignoredOracleErrorNumber)
		{
			// Expected and documented: see the call sites.
		}
	}

	private static string LoadShipped(string fileName)
	{
		var assembly = Assembly.GetExecutingAssembly();

		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(fileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{fileName}' is not embedded in {assembly.GetName().Name}. It is linked "
				+ "in by the test project's EmbeddedResource item; if that item was removed, this suite would "
				+ "silently fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
