// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Provisions the SINGLE-TENANT inbox schema from the DDL the package ships, rather than from a
/// hand-written copy.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the event-store equivalent, for the same reason: a hand-written copy of a shipped schema
/// drifts, and the drift is invisible until a write hits a column the copy never had. Linking the real
/// script also means the artifact consumers actually run is exercised at all.
/// </para>
/// <para>
/// The shipped script hardcodes <c>[dbo].[inbox_messages]</c> — the same name the shared multi-tenant
/// inbox fixture uses — so the two shapes cannot coexist in one database. Callers therefore provision a
/// DEDICATED database on the shared container and run the script there verbatim. Rewriting the script's
/// table name to dodge the collision would re-introduce exactly the gap this type exists to close.
/// </para>
/// </remarks>
internal static class ShippedInboxSchema
{
	private const string ScriptFileName = "001_CreateInboxSchema.sql";

	/// <summary>
	/// Gets the single-tenant inbox DDL as shipped in the package.
	/// </summary>
	public static string Ddl { get; } = LoadShippedDdl();

	/// <summary>
	/// Creates <paramref name="databaseName"/> on the target server if absent, provisions the shipped
	/// single-tenant inbox schema inside it, and returns a connection string scoped to that database.
	/// </summary>
	/// <param name="serverConnectionString">A connection string for the shared container.</param>
	/// <param name="databaseName">The dedicated database to create and provision.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A connection string targeting the provisioned database.</returns>
	public static async Task<string> EnsureDatabaseAndSchemaAsync(
		string serverConnectionString,
		string databaseName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

		await using (var serverConnection = new SqlConnection(serverConnectionString))
		{
			await serverConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

			// CREATE DATABASE cannot be parameterised; the name is a compile-time constant supplied by the
			// suite, never user input. Bracket-quoted so it is a single identifier regardless.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var create = new SqlCommand(
				$"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}];",
				serverConnection);
#pragma warning restore CA2100
			_ = await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}

		var scoped = new SqlConnectionStringBuilder(serverConnectionString)
		{
			InitialCatalog = databaseName,
		}.ConnectionString;

		await using (var connection = new SqlConnection(scoped))
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			// The shipped script is a bare CREATE, written to be run once by hand against a fresh database.
			// Suites re-enter per class, so the existence check lives here rather than in the script —
			// altering the script to suit the tests would mean the thing under test is no longer the thing
			// we ship.
			await using (var probe = new SqlCommand(
				"SELECT CASE WHEN OBJECT_ID('[dbo].[inbox_messages]', 'U') IS NULL THEN 0 ELSE 1 END",
				connection))
			{
				if ((int)(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1)
				{
					return scoped;
				}
			}

			// GO is a batch separator understood by sqlcmd and SSMS, not T-SQL: SqlCommand rejects it.
			foreach (var batch in SplitBatches(Ddl))
			{
#pragma warning disable CA2100 // DDL embedded at compile time from the package's own script.
				await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		return scoped;
	}

	/// <summary>
	/// Deletes every row from the provisioned single-tenant inbox table.
	/// </summary>
	/// <param name="connectionString">A connection string scoped to the provisioned database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ResetAsync(string connectionString, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand("DELETE FROM [dbo].[inbox_messages];", connection);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static IEnumerable<string> SplitBatches(string script) =>
		script
			.Split(["\nGO\r\n", "\nGO\n", "\r\nGO\r\n"], StringSplitOptions.None)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0 && !batch.Equals("GO", StringComparison.OrdinalIgnoreCase));

	private static string LoadShippedDdl()
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix rather than a hardcoded manifest name: the resource name is derived from the
		// link path, so pinning the full name would make an unrelated project restructure fail here.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(ScriptFileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped schema '{ScriptFileName}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, " +
				"this suite would silently fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
