// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Provisions the event-store table from the DDL the package actually ships.
/// </summary>
/// <remarks>
/// <para>
/// Every SQL Server event-store suite used to carry its own hand-written <c>CREATE TABLE</c>. Those
/// copies drifted from the shipped script, and the drift was invisible until it wasn't: the tenancy
/// column landed in the store's SQL and in the shipped script, and four of the seven test copies were
/// never updated, so every append and load in those suites failed with
/// <c>Invalid column name 'TenantId'</c>. One copy was then repaired by hand — which left the other
/// three broken and kept the underlying duplication in place.
/// </para>
/// <para>
/// The drift is the symptom; the duplicate schema definition is the cause. Provisioning from the
/// shipped file removes the second definition, so there is nothing left to diverge: a column added to
/// the product's schema reaches these tests automatically, and one that isn't added fails them.
/// </para>
/// <para>
/// It also closes a coverage gap that mattered more than the failures. Because the suites built their
/// own schema, <em>nothing</em> exercised the script consumers actually run — a typo in it would have
/// shipped with a full green suite behind it. These tests now run against that file, so it is covered
/// by construction.
/// </para>
/// </remarks>
internal static class ShippedEventStoreSchema
{
	private const string ScriptFileName = "001_CreateEventStoreSchema.sql";

	/// <summary>
	/// Gets the DDL text as shipped in the package.
	/// </summary>
	public static string Ddl { get; } = LoadShippedDdl();

	/// <summary>
	/// Creates the event-store table if it is not already present.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// The shipped script is a bare CREATE — it is written to be run once, by hand, against a fresh
		// database. Suites re-enter this per class, so the existence check lives here rather than being
		// added to the script: altering the script to suit the tests would re-introduce the very gap
		// this type exists to close (the thing under test would no longer be the thing we ship).
		await using (var probe = new SqlCommand(
			"SELECT CASE WHEN OBJECT_ID('[dbo].[EventStoreEvents]', 'U') IS NULL THEN 0 ELSE 1 END", connection))
		{
			if ((int)(await probe.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1)
			{
				return;
			}
		}

		// GO is a batch separator understood by sqlcmd and SSMS, not T-SQL: SqlCommand rejects it.
		foreach (var batch in SplitBatches(Ddl))
		{
			// CA2100: the command text is DDL embedded in this assembly at compile time from the
			// package's own script. It is not reachable from user input, and it cannot be parameterised
			// — object definitions are not parameterisable in T-SQL. Scoped to this statement rather
			// than the file so a genuine concatenation added later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Deletes every row and restarts the global position ordinal.
	/// </summary>
	/// <remarks>
	/// Range-query assertions compare absolute <c>Position</c> values, so the IDENTITY seed has to be
	/// deterministic regardless of execution order. DELETE alone does not reset it.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ResetAsync(string connectionString, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = new SqlCommand(
			"DELETE FROM [dbo].[EventStoreEvents]; DBCC CHECKIDENT ('[dbo].[EventStoreEvents]', RESEED, 0);",
			connection);
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

		// Matched by suffix rather than by a hardcoded manifest name: the resource name is derived from
		// the link path, so pinning the full name would make an unrelated project restructure fail here
		// with a null stream instead of a sentence.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(ScriptFileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped schema '{ScriptFileName}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, " +
				"these suites would silently fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
