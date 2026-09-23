// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Data.SqlClient;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Provisions the grant tables from the DDL the provider packages ship.
/// </summary>
/// <remarks>
/// Nothing here restates a <c>CREATE TABLE</c>. Neither grant store creates its tables at runtime, so a suite
/// that provisioned its own definition would certify the store against a schema no consumer has.
/// </remarks>
[SuppressMessage(
	"Security",
	"CA2100:Review SQL queries for security vulnerabilities",
	Justification = "The command text is DDL read from a script shipped with the package, or a fixed database "
					+ "name, not user input.")]
internal static class ShippedGrantSchema
{
	private const string PostgresScript = "Postgres.003_CreateGrantSchema.sql";
	private const string SqlServerScript = "SqlServer.003_CreateGrantSchema.sql";

	/// <summary>The binary collation the shipped SQL Server script puts on every identity column.</summary>
	public const string SqlServerBinaryCollation = " COLLATE Latin1_General_BIN2";

	/// <summary>Provisions the Postgres grant tables from the shipped script.</summary>
	public static async Task ProvisionPostgresAsync(string connectionString, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = LoadShipped(PostgresScript);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Provisions the SQL Server grant tables from the shipped script, submitted as ONE command: the script
	/// promises it is a single batch, and running it this way holds it to that.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="stripBinaryCollation">
	/// <see langword="true"/> to remove the script's column collation, so the tables inherit the database's
	/// default -- the shape of a table a consumer created by hand. Used only to prove the store's own
	/// comparisons stay exact on such a table.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ProvisionSqlServerAsync(
		string connectionString, bool stripBinaryCollation, CancellationToken cancellationToken)
	{
		var script = LoadShipped(SqlServerScript);
		if (stripBinaryCollation)
		{
			script.Contains(SqlServerBinaryCollation, StringComparison.Ordinal).ShouldBeTrue(
				"the shipped script no longer declares the binary collation this variant strips");
			script = script.Replace(SqlServerBinaryCollation, string.Empty, StringComparison.Ordinal);
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = script;
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Creates (once) a database whose default collation is case-INSENSITIVE, and returns its connection string.
	/// </summary>
	public static async Task<string> EnsureCaseInsensitiveSqlServerDatabaseAsync(
		string connectionString, CancellationToken cancellationToken)
	{
		const string database = "excalibur_grants_ci";

		await using (var connection = new SqlConnection(connectionString))
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			await using var command = connection.CreateCommand();
			command.CommandText =
				$"IF DB_ID(N'{database}') IS NULL CREATE DATABASE [{database}] COLLATE SQL_Latin1_General_CP1_CI_AS;";
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		return new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database }.ConnectionString;
	}

	private static string LoadShipped(string scriptSuffix)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix INCLUDING the dialect folder: both engines ship a file of this name.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(scriptSuffix, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{scriptSuffix}' is not embedded in {assembly.GetName().Name}. It is linked in "
				+ "by the test project's EmbeddedResource item; without it these arms would have no shipped schema.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
