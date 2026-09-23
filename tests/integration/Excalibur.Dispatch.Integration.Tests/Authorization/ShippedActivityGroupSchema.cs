// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Data.SqlClient;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Provisions the activity-group table from the DDL the provider packages ship.
/// </summary>
/// <remarks>
/// Nothing here restates a <c>CREATE TABLE</c>. Neither activity-group store creates its table at
/// runtime, so a suite that provisioned its own definition would certify the store against a schema no
/// consumer has. Loading the shipped script is what makes these arms evidence about what a consumer runs.
/// </remarks>
[SuppressMessage(
	"Security",
	"CA2100:Review SQL queries for security vulnerabilities",
	Justification = "The command text is DDL read from a script shipped with the package, not user input. "
					+ "Parameterising it is not possible and would defeat the point: these arms exist to run "
					+ "the exact script a consumer runs.")]
internal static class ShippedActivityGroupSchema
{
	private const string PostgresScript = "Postgres.002_CreateActivityGroupSchema.sql";
	private const string SqlServerScript = "SqlServer.002_CreateActivityGroupSchema.sql";

	/// <summary>Provisions the Postgres activity-group table from the shipped script.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">
	/// The schema to create the table in. Anything but the default runs the script with <c>authz</c> replaced,
	/// which is what the store's documentation tells a consumer to do.
	/// </param>
	public static Task ProvisionPostgresAsync(string connectionString, CancellationToken cancellationToken) =>
		ProvisionPostgresAsync(connectionString, "authz", cancellationToken);

	/// <inheritdoc cref="ProvisionPostgresAsync(string, CancellationToken)"/>
	public static async Task ProvisionPostgresAsync(
		string connectionString,
		string schema,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = LoadShipped(PostgresScript).Replace("authz", schema, StringComparison.Ordinal);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Provisions the SQL Server activity-group table from the shipped script.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// Submitted as ONE command, deliberately, and not split on <c>GO</c>. The script states that it is a
	/// single batch so a caller who runs the whole file as one command succeeds; running it that way here
	/// is what holds it to that promise. Were a <c>GO</c> to be added, this would fail.
	/// </remarks>
	/// <param name="schema">
	/// The schema to create the table in. Anything but the default runs the script with <c>authz</c> replaced,
	/// which is what the store's documentation tells a consumer to do.
	/// </param>
	public static Task ProvisionSqlServerAsync(string connectionString, CancellationToken cancellationToken) =>
		ProvisionSqlServerAsync(connectionString, "authz", cancellationToken);

	/// <inheritdoc cref="ProvisionSqlServerAsync(string, CancellationToken)"/>
	public static async Task ProvisionSqlServerAsync(
		string connectionString,
		string schema,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = LoadShipped(SqlServerScript).Replace("authz", schema, StringComparison.Ordinal);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Empties the Postgres activity-group table between arms.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema the table was provisioned in.</param>
	public static Task TruncatePostgresAsync(string connectionString, CancellationToken cancellationToken) =>
		TruncatePostgresAsync(connectionString, "authz", cancellationToken);

	/// <inheritdoc cref="TruncatePostgresAsync(string, CancellationToken)"/>
	public static async Task TruncatePostgresAsync(
		string connectionString,
		string schema,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = $"DELETE FROM \"{schema}\".\"activity_group\"";
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Empties the SQL Server activity-group table between arms.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema the table was provisioned in.</param>
	public static Task TruncateSqlServerAsync(string connectionString, CancellationToken cancellationToken) =>
		TruncateSqlServerAsync(connectionString, "authz", cancellationToken);

	/// <inheritdoc cref="TruncateSqlServerAsync(string, CancellationToken)"/>
	public static async Task TruncateSqlServerAsync(
		string connectionString,
		string schema,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = $"DELETE FROM [{schema}].[ActivityGroup]";
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static string LoadShipped(string scriptSuffix)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix INCLUDING the dialect folder: both engines ship a file of this name, so a
		// bare leaf would match either and these arms would silently provision the wrong dialect.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(scriptSuffix, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{scriptSuffix}' is not embedded in {assembly.GetName().Name}. It is "
				+ "linked in by the test project's EmbeddedResource item; if that item was removed, these arms "
				+ "would fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
