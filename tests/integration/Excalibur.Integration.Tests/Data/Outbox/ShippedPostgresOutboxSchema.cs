// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Provisions the Postgres outbox tables from the DDL the package actually ships.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PostgresOutboxStoreContainerFixture"/> hand-writes its own <c>CREATE TABLE</c>. That is
/// a reasonable choice for the store's behavioural suites — they care about the store's SQL, not about
/// provisioning — but it has a consequence worth stating plainly: nothing in the tree exercised the
/// script a consumer runs. A column that the shipped script declares differently from the fixture
/// would be invisible behind a fully green suite.
/// </para>
/// <para>
/// This type closes that gap for the properties that are decided by the SCHEMA rather than by the
/// store: whether a <c>DEFAULT</c> fires on an omitted column, and whether the column refuses NULL.
/// Neither can be answered by a copy of the DDL — only by the file itself.
/// </para>
/// <para>
/// The script is embedded from <c>src/</c> by the test project rather than copied here, for the same
/// reason: a copy would let the shipped script rot behind a green suite.
/// </para>
/// <para>
/// There is no upgrade script to run. The package ships one CREATE script per provider, already at the
/// final shape, and a consumer holding an older database re-provisions from it.
/// </para>
/// </remarks>
internal static class ShippedPostgresOutboxSchema
{
	private const string CreateScriptFileName = "001_CreatePostgresOutboxSchema.sql";

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
		await ExecuteAsync(
			connectionString,
			"""
			DROP TABLE IF EXISTS public.outbox;
			DROP TABLE IF EXISTS public.outbox_dead_letters;
			DROP TABLE IF EXISTS public.outbox_fence;
			""",
			cancellationToken).ConfigureAwait(false);

		await ExecuteAsync(connectionString, CreateDdl, cancellationToken).ConfigureAwait(false);
	}

	private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// CA2100: the command text is the package's own DDL, embedded at compile time, plus fixed
		// literals in this file. It is not reachable from user input, and object definitions cannot be
		// parameterised. Scoped to this statement so a genuine concatenation added later is reported.
#pragma warning disable CA2100
		await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static string LoadShipped(string fileName)
	{
		var assembly = Assembly.GetExecutingAssembly();

		// Matched by suffix rather than by a hardcoded manifest name: the resource name is derived from
		// the link path, so pinning the full name would make an unrelated project restructure fail here
		// with a null stream instead of a sentence.
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
