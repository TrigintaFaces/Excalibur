// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Migrate.Tool.Commands;

/// <summary>
/// Handler for the 'status' command that shows migration status.
/// </summary>
internal sealed class StatusCommandHandler
{
	/// <summary>
	/// Executes the status command to display applied and pending migrations.
	/// </summary>
	/// <param name="provider">Database provider.</param>
	/// <param name="connectionString">Connection string.</param>
	/// <param name="assemblyPath">Optional assembly path.</param>
	/// <param name="migrationNamespace">Optional migration namespace.</param>
	/// <param name="verbose">Whether to enable verbose output.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		string provider,
		string connectionString,
		string? assemblyPath,
		string? migrationNamespace,
		bool verbose)
	{
		Console.WriteLine($"Migration status for {provider}:");
		Console.WriteLine(new string('-', 60));

		using var migratorResult = MigratorFactory.CreateMigrator(provider, connectionString, assemblyPath, migrationNamespace, verbose);
		var migrator = migratorResult.Migrator;

		var appliedMigrations = await migrator.GetAppliedMigrationsAsync(CancellationToken.None).ConfigureAwait(false);
		var pendingMigrations = await migrator.GetPendingMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"Applied Migrations ({appliedMigrations.Count}):");
		Console.ResetColor();

		if (appliedMigrations.Count == 0)
		{
			Console.WriteLine("  (none)");
		}
		else
		{
			foreach (var migration in appliedMigrations)
			{
				Console.Write("  [");
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Write("APPLIED");
				Console.ResetColor();
				Console.Write("] ");
				Console.WriteLine($"{migration.MigrationId}");

				if (verbose)
				{
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.WriteLine($"          Applied: {migration.AppliedAt:yyyy-MM-dd HH:mm:ss}");
					if (!string.IsNullOrWhiteSpace(migration.Description))
					{
						Console.WriteLine($"          Description: {migration.Description}");
					}

					if (!string.IsNullOrWhiteSpace(migration.Checksum))
					{
						Console.WriteLine($"          Checksum: {migration.Checksum}");
					}

					Console.ResetColor();
				}
			}
		}

		Console.WriteLine();
		Console.ForegroundColor = ConsoleColor.Yellow;
		Console.WriteLine($"Pending Migrations ({pendingMigrations.Count}):");
		Console.ResetColor();

		if (pendingMigrations.Count == 0)
		{
			Console.WriteLine("  (none - database is up to date)");
		}
		else
		{
			foreach (var migrationId in pendingMigrations)
			{
				Console.Write("  [");
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Write("PENDING");
				Console.ResetColor();
				Console.Write("] ");
				Console.WriteLine(migrationId);
			}
		}

		Console.WriteLine();
		Console.WriteLine(new string('-', 60));

		if (pendingMigrations.Count > 0)
		{
			Console.WriteLine($"Run 'excalibur-migrate up' to apply {pendingMigrations.Count} pending migration(s).");
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Database is up to date!");
			Console.ResetColor();
		}
	}
}
