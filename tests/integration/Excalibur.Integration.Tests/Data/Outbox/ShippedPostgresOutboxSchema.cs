// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Npgsql;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Provisions the Postgres outbox tables from the DDL the package actually ships, and runs the
/// upgrade script the package actually ships.
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
/// store: whether a <c>DEFAULT</c> fires on an omitted column, whether the column refuses NULL, and
/// whether the upgrade script converges an older database. None of those can be answered by a copy of
/// the DDL — only by the file itself.
/// </para>
/// <para>
/// Both scripts are embedded from <c>src/</c> by the test project rather than copied here, for the
/// same reason: a copy would let the shipped script rot behind a green suite.
/// </para>
/// </remarks>
internal static class ShippedPostgresOutboxSchema
{
	private const string CreateScriptFileName = "001_CreatePostgresOutboxSchema.sql";
	private const string MigrationScriptFileName = "002_MakePostgresOutboxTenantTotal.sql";
	private const string DeadLetterMigrationScriptFileName = "003_CarryPostgresDeadLetterTenant.sql";

	/// <summary>
	/// Gets the shipped fresh-install DDL.
	/// </summary>
	public static string CreateDdl { get; } = LoadShipped(CreateScriptFileName);

	/// <summary>
	/// Gets the shipped tenant-totality upgrade script.
	/// </summary>
	public static string MigrationDdl { get; } = LoadShipped(MigrationScriptFileName);

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

	/// <summary>
	/// Runs the shipped tenant-totality upgrade script.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RunMigrationAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteAsync(connectionString, MigrationDdl, cancellationToken);

	/// <summary>
	/// Gets the shipped dead-letter tenant-provenance upgrade script.
	/// </summary>
	public static string DeadLetterMigrationDdl { get; } = LoadShipped(DeadLetterMigrationScriptFileName);

	/// <summary>
	/// Runs the shipped dead-letter tenant-provenance upgrade script.
	/// </summary>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RunDeadLetterMigrationAsync(string connectionString, CancellationToken cancellationToken) =>
		ExecuteAsync(connectionString, DeadLetterMigrationDdl, cancellationToken);

	/// <summary>
	/// Re-opens the dead-letter table to its pre-wave shape: no tenant column, and a primary key on the
	/// message id alone.
	/// </summary>
	/// <remarks>
	/// Reconstructed from the CURRENT shipped schema rather than hand-written, for the same reason as the
	/// outbox equivalent below: it creates today's table and then removes the one column, which is exactly
	/// the state a database provisioned before this wave is in. Dropping the column takes the composite key
	/// with it, so the single-column key is restored explicitly under Postgres's implicit name — the name an
	/// older create script would have produced, which is what makes the migration's drop-by-real-name step
	/// non-trivial rather than a formality.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task RemoveDeadLetterTenantColumnToLegacyShapeAsync(
		string connectionString, CancellationToken cancellationToken) =>
		ExecuteAsync(
			connectionString,
			"""
			ALTER TABLE public.outbox_dead_letters DROP COLUMN tenant_id;
			ALTER TABLE public.outbox_dead_letters
			    ADD CONSTRAINT outbox_dead_letters_pkey PRIMARY KEY (message_id);
			""",
			cancellationToken);

	/// <summary>
	/// Re-opens the tenant column to its pre-wave shape: nullable, with no default.
	/// </summary>
	/// <remarks>
	/// The legacy shape is reconstructed from the CURRENT shipped schema rather than hand-written, so
	/// this cannot drift from the product: it creates today's table and then re-opens the one column,
	/// which is exactly the state a database created before this wave is in.
	/// </remarks>
	/// <param name="connectionString">The target database.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public static Task ReopenTenantColumnToLegacyShapeAsync(
		string connectionString, CancellationToken cancellationToken) =>
		ExecuteAsync(
			connectionString,
			"""
			ALTER TABLE public.outbox ALTER COLUMN tenant_id DROP NOT NULL;
			ALTER TABLE public.outbox ALTER COLUMN tenant_id DROP DEFAULT;
			""",
			cancellationToken);

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
