// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Data.SqlClient;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

/// <summary>
/// Provisions the grant table the authorization grant stores read and write.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here restates a <c>CREATE TABLE</c>.</b> The provider packages ship the grant DDL, so
/// provisioning delegates to <see cref="ShippedGrantSchema"/> and these arms certify the stores against the
/// schema a consumer actually runs. A second definition here would drift from it silently — and did: a
/// hand-written copy declared a narrower qualifier than the shipped script, and whichever fixture created the
/// table first decided what every other arm in the assembly measured.
/// </para>
/// <para>
/// <b>The shipped script's primary key is load-bearing for these arms, not decoration.</b> Two replaces that
/// are not serialized each delete what they can see and then insert, so the second one inserts rows the first
/// has already written — which is a key violation on the shipped table and a silent duplicate on a table
/// without the key. The loud failure is what makes the concurrency arms able to fail.
/// </para>
/// </remarks>
[SuppressMessage(
	"Security",
	"CA2100:Review SQL queries for security vulnerabilities",
	Justification = "The command text is fixed DDL declared in this file, not user input.")]
internal static class GrantSchema
{
	/// <summary>Creates the SQL Server grant table if it does not exist.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table exists.</returns>
	public static Task ProvisionSqlServerAsync(string connectionString, CancellationToken cancellationToken) =>
		ShippedGrantSchema.ProvisionSqlServerAsync(connectionString, stripBinaryCollation: false, cancellationToken);

	/// <summary>Empties the SQL Server grant table.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table is empty.</returns>
	public static Task TruncateSqlServerAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteSqlServerAsync(connectionString, "DELETE FROM [authz].[Grant]", cancellationToken);

	/// <summary>Creates the PostgreSQL grant table if it does not exist.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table exists.</returns>
	public static Task ProvisionPostgresAsync(string connectionString, CancellationToken cancellationToken) =>
		ShippedGrantSchema.ProvisionPostgresAsync(connectionString, cancellationToken);

	/// <summary>Empties the PostgreSQL grant table.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the table is empty.</returns>
	public static Task TruncatePostgresAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecutePostgresAsync(connectionString, "DELETE FROM \"authz\".\"grant\"", cancellationToken);

	/// <summary>Reads the qualifiers a user holds, from SQL Server.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="userId">The user to read.</param>
	/// <param name="grantType">The grant type to read.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The qualifiers, in no particular order.</returns>
	public static async Task<IReadOnlyList<string>> SqlServerQualifiersAsync(
		string connectionString,
		string userId,
		string grantType,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT Qualifier FROM [authz].[Grant] WHERE UserId = @u AND GrantType = @g";
		_ = command.Parameters.AddWithValue("@u", userId);
		_ = command.Parameters.AddWithValue("@g", grantType);

		return await ReadQualifiersAsync(command, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Reads the qualifiers a user holds, from PostgreSQL.</summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="userId">The user to read.</param>
	/// <param name="grantType">The grant type to read.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The qualifiers, in no particular order.</returns>
	public static async Task<IReadOnlyList<string>> PostgresQualifiersAsync(
		string connectionString,
		string userId,
		string grantType,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT qualifier FROM \"authz\".\"grant\" WHERE user_id = @u AND grant_type = @g";
		_ = command.Parameters.AddWithValue("u", userId);
		_ = command.Parameters.AddWithValue("g", grantType);

		return await ReadQualifiersAsync(command, cancellationToken).ConfigureAwait(false);
	}

	private static async Task<IReadOnlyList<string>> ReadQualifiersAsync(
		System.Data.Common.DbCommand command,
		CancellationToken cancellationToken)
	{
		var qualifiers = new List<string>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			qualifiers.Add(reader.GetString(0));
		}

		return qualifiers;
	}

	private static async Task ExecuteSqlServerAsync(
		string connectionString,
		string sql,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static async Task ExecutePostgresAsync(
		string connectionString,
		string sql,
		CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}
}
