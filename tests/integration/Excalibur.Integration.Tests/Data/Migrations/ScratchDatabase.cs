// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Migrations;

/// <summary>
/// An empty PostgreSQL database, created inside the shared container and dropped when the test ends.
/// </summary>
/// <remarks>
/// The shipped migrations name their schemas and tables as literals, so two arms running against one
/// database would fight over the same objects. Isolating at the DATABASE level rather than the schema
/// level is what lets each arm start from genuinely nothing and lets a migration create the schema
/// itself, which is the state a consumer's database is actually in.
/// </remarks>
internal sealed class ScratchDatabase : IAsyncDisposable
{
	private readonly string _adminConnectionString;
	private readonly string _name;

	private ScratchDatabase(string adminConnectionString, string name, string connectionString)
	{
		_adminConnectionString = adminConnectionString;
		_name = name;
		ConnectionString = connectionString;
	}

	/// <summary>
	/// Gets the connection string addressing the scratch database.
	/// </summary>
	public string ConnectionString { get; }

	/// <summary>
	/// Creates a fresh, empty database whose name is derived from <paramref name="label"/>.
	/// </summary>
	public static async Task<ScratchDatabase> CreateAsync(
		string adminConnectionString,
		string label,
		CancellationToken cancellationToken)
	{
		var name = Sanitize(label);

		await using (var admin = new NpgsqlConnection(adminConnectionString))
		{
			await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

			// DROP first: a run that was killed between arms leaves the database behind, and inheriting a
			// half-migrated one would make the next run's result depend on the previous run's crash.
			await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", cancellationToken)
				.ConfigureAwait(false);
			await ExecuteAsync(admin, $"CREATE DATABASE \"{name}\"", cancellationToken).ConfigureAwait(false);
		}

		var target = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name }
			.ConnectionString;

		return new ScratchDatabase(adminConnectionString, name, target);
	}

	public async ValueTask DisposeAsync()
	{
		NpgsqlConnection.ClearAllPools();

		await using var admin = new NpgsqlConnection(_adminConnectionString);
		await admin.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", CancellationToken.None)
			.ConfigureAwait(false);
	}

	private static async Task ExecuteAsync(
		NpgsqlConnection connection,
		string sql,
		CancellationToken cancellationToken)
	{
		// CA2100: callers pass a const string with an identifier this class sanitises and quotes itself.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Reduces a label to characters that are safe in a quoted identifier, and to a length PostgreSQL
	/// will not truncate.
	/// </summary>
	private static string Sanitize(string label)
	{
		var cleaned = new string([.. label.Select(static c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')]);

		// PostgreSQL truncates identifiers at 63 bytes. Truncating here rather than letting the server do
		// it keeps the name we DROP identical to the name we CREATED.
		var suffix = Math.Abs(label.GetHashCode(StringComparison.Ordinal))
			.ToString(CultureInfo.InvariantCulture);
		var head = cleaned.Length > 40 ? cleaned[^40..] : cleaned;

		return $"mig_{head}_{suffix}";
	}
}
