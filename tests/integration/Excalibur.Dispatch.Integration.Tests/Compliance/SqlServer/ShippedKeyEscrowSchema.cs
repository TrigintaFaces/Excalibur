// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Provisions the key-escrow tables from the DDL the package actually ships.
/// </summary>
/// <remarks>
/// <para>
/// Every key-escrow suite carried its own hand-written <c>CREATE TABLE</c>, and for a long time those
/// copies were the <em>only</em> definition of this schema that existed anywhere. The package shipped
/// no script for these three tables at all, so a consumer who enabled escrow had no way to create them
/// — while the suites passed, because they built their own.
/// </para>
/// <para>
/// That is the failure this type exists to make impossible. The duplicate definition is the cause; the
/// drift is only the symptom. Provisioning from the shipped file means there is no second definition
/// left to diverge from, and — more importantly — the script a consumer runs is now covered by
/// construction. Before this, a missing column in it would have shipped behind a fully green suite.
/// </para>
/// <para>
/// The copies in the sibling suites are deliberately left alone: they override the table names to
/// exercise that configuration path. This type is the one that binds the DEFAULTS, which is what a
/// consumer gets.
/// </para>
/// </remarks>
internal static class ShippedKeyEscrowSchema
{
	private const string ScriptFileName = "002_CreateKeyEscrowSchema.sql";

	/// <summary>
	/// Gets the DDL text as shipped in the package.
	/// </summary>
	public static string Ddl { get; } = LoadShippedScript(ScriptFileName);

	/// <summary>
	/// Runs any script the package ships, by file name, against the target database.
	/// </summary>
	/// <remarks>
	/// The migrations belong here for the same reason the create script does: a suite that applied its
	/// own hand-written ALTER would be asserting something about a statement no consumer runs. Loading
	/// by file name keeps the shipped file the only copy that exists.
	/// </remarks>
	/// <param name="scriptFileName">The shipped script's file name.</param>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task ApplyShippedScriptAsync(
		string scriptFileName, string connectionString, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scriptFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		foreach (var batch in SplitBatches(LoadShippedScript(scriptFileName)))
		{
			// CA2100: the command text is the package's own script, embedded at compile time.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates the key-escrow tables if they are not already present.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static async Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// No existence probe here, unlike the event-store equivalent: every statement in the shipped
		// escrow script is already guarded, so re-entry per class is safe against the file as shipped.
		// Re-running it is itself part of what these suites assert about the script.
		foreach (var batch in SplitBatches(Ddl))
		{
			// CA2100: the command text is the package's own DDL, embedded at compile time. It is not
			// reachable from user input and object definitions cannot be parameterised in T-SQL. Scoped
			// to this statement so a genuine concatenation added later is still reported.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
			_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private static IEnumerable<string> SplitBatches(string script) =>
		script
			.Split(["\nGO\r\n", "\nGO\n", "\r\nGO\r\n"], StringSplitOptions.None)
			.Select(static batch => batch.Trim())
			.Where(static batch => batch.Length > 0 && !batch.Equals("GO", StringComparison.OrdinalIgnoreCase));

	private static string LoadShippedScript(string fileName)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix rather than a hardcoded manifest name: the resource name is derived from the
		// link path, so pinning the full name would turn an unrelated restructure into a null stream
		// instead of a sentence.
		var resourceName = Array.Find(
			assembly.GetManifestResourceNames(),
			name => name.EndsWith(fileName, StringComparison.Ordinal))
			?? throw new InvalidOperationException(
				$"The shipped script '{fileName}' is not embedded in {assembly.GetName().Name}. " +
				"It is linked in by the test project's EmbeddedResource item; if that item was removed, " +
				"these suites would silently fall back to a schema no consumer has.");

		using var stream = assembly.GetManifestResourceStream(resourceName)!;
		using var reader = new StreamReader(stream);

		return reader.ReadToEnd();
	}
}
