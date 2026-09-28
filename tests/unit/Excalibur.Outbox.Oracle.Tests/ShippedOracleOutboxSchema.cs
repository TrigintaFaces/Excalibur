// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Provisions the Oracle outbox tables from the DDL the package ships.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OracleOutboxStoreContainerFixture"/> hand-writes its own <c>CREATE TABLE</c>. That is
/// reasonable for the store's behavioural suites, but it means nothing in the tree exercised the
/// script a consumer actually runs. A defect in that script is not a compile-time defect anywhere in
/// this repository; it is discovered by running it, or by a consumer.
/// </para>
/// <para>
/// There is no upgrade script to run. The package ships one CREATE script per provider, already at the
/// final shape, and a consumer holding an older database re-provisions from it.
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

	/// <summary>
	/// Gets the shipped fresh-install DDL.
	/// </summary>
	public static string CreateDdl { get; } = LoadShipped(CreateScriptFileName);

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
