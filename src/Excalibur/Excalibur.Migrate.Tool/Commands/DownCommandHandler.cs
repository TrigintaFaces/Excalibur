// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Migrate.Tool.Commands;

/// <summary>
/// Handler for the 'down' command that rolls back migrations.
/// </summary>
internal sealed class DownCommandHandler
{
	/// <summary>
	/// Executes the down command to roll back migrations.
	/// </summary>
	/// <param name="provider">Database provider.</param>
	/// <param name="connectionString">Connection string.</param>
	/// <param name="assemblyPath">Optional assembly path.</param>
	/// <param name="migrationNamespace">Optional migration namespace.</param>
	/// <param name="verbose">Whether to enable verbose output.</param>
	/// <param name="targetMigrationId">The target migration ID to roll back to.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		string provider,
		string connectionString,
		string? assemblyPath,
		string? migrationNamespace,
		bool verbose,
		string targetMigrationId)
	{
		Console.WriteLine($"Rolling back to migration {targetMigrationId} using {provider}...");

		using var migratorResult = MigratorFactory.CreateMigrator(provider, connectionString, assemblyPath, migrationNamespace, verbose);
		var migrator = migratorResult.Migrator;

		// First, show current state
		var appliedMigrations = await migrator.GetAppliedMigrationsAsync(CancellationToken.None).ConfigureAwait(false);
		var targetIndex = -1;

		for (var i = 0; i < appliedMigrations.Count; i++)
		{
			if (string.Equals(appliedMigrations[i].MigrationId, targetMigrationId, StringComparison.Ordinal))
			{
				targetIndex = i;
				break;
			}
		}

		if (targetIndex < 0)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine($"Target migration '{targetMigrationId}' not found in applied migrations.");
			Console.ResetColor();

			Console.WriteLine("\nApplied migrations:");
			foreach (var migration in appliedMigrations)
			{
				Console.WriteLine($"  - {migration.MigrationId} (applied: {migration.AppliedAt:yyyy-MM-dd HH:mm:ss})");
			}

			throw new InvalidOperationException($"Target migration '{targetMigrationId}' not found.");
		}

		var migrationsToRemove = appliedMigrations.Count - targetIndex - 1;
		if (migrationsToRemove == 0)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("No migrations to roll back. The target migration is the latest applied migration.");
			Console.ResetColor();
			return;
		}

		Console.WriteLine($"This will remove {migrationsToRemove} migration(s):");
		for (var i = appliedMigrations.Count - 1; i > targetIndex; i--)
		{
			Console.WriteLine($"  - {appliedMigrations[i].MigrationId}");
		}

		var result = await migrator.RollbackAsync(targetMigrationId, CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"Successfully rolled back {migrationsToRemove} migration(s).");
			Console.ResetColor();
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine($"Rollback failed: {result.ErrorMessage}");
			Console.ResetColor();

			if (result.Exception != null && verbose)
			{
				Console.Error.WriteLine(result.Exception.ToString());
			}

			throw new InvalidOperationException($"Rollback failed: {result.ErrorMessage}", result.Exception);
		}
	}
}
