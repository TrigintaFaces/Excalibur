// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Data.SqlClient;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.DeadLetter;

/// <summary>
/// Provisions the dead-letter tables from the DDL the packages ship, for the conformance suites.
/// </summary>
/// <remarks>
/// Nothing here restates a <c>CREATE TABLE</c>. A suite that provisioned its own definition would pass
/// against a schema no consumer has, and neither dead-letter store creates its table at runtime -- the
/// Postgres script says so in its own header. Loading the shipped script is what makes these suites
/// evidence about what a consumer will actually run.
/// </remarks>
[SuppressMessage(
	"Security",
	"CA2100:Review SQL queries for security vulnerabilities",
	Justification = "The command text is DDL read from an embedded resource shipped with the package, not user input. "
					+ "Parameterising it is not possible and would defeat the point: these suites exist to run the exact "
					+ "script a consumer runs.")]
internal static class ShippedDeadLetterSchema
{
	private const string PostgresScript = "Postgres.001_CreateDeadLetterSchema.sql";
	private const string SqlServerScript = "SqlServer.001_CreateDeadLetterSchema.sql";

	/// <summary>Provisions the Postgres dead-letter table from the shipped script.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ProvisionPostgresAsync(string connectionString, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = LoadShipped(PostgresScript);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Provisions the SQL Server dead-letter table from the shipped script.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ProvisionSqlServerAsync(string connectionString, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// The shipped script is batched by GO, which is a client-side separator the driver does not
		// understand. Splitting on it is what lets the suite run the file a consumer runs.
		foreach (var batch in SplitBatches(LoadShipped(SqlServerScript)))
		{
			await using var command = connection.CreateCommand();
			command.CommandText = batch;
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Empties the Postgres dead-letter table between arms.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task TruncatePostgresAsync(string connectionString, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM \"public\".\"dead_letter_messages\"";
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Empties the SQL Server dead-letter table between arms.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task TruncateSqlServerAsync(string connectionString, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM [dbo].[DeadLetterMessages]";
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static IEnumerable<string> SplitBatches(string script) =>
		script
			.Split(["\nGO\r\n", "\nGO\n", "\r\nGO\r\n"], StringSplitOptions.RemoveEmptyEntries)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0);

	private static string LoadShipped(string scriptSuffix)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix including the dialect folder: both engines ship a file named
		// 001_CreateDeadLetterSchema.sql, so a bare leaf would match either and this suite would
		// silently provision the wrong dialect.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(scriptSuffix, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{scriptSuffix}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, " +
				"these suites would fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
