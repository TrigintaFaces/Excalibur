// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Migrate.Tool.Commands;

/// <summary>
/// Handler for the 'up' command that applies pending migrations.
/// </summary>
internal sealed class UpCommandHandler
{
	/// <summary>
	/// Executes the up command to apply pending migrations.
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
		Console.Out.WriteLine($"Applying migrations using {provider}...");

		using var migratorResult = MigratorFactory.CreateMigrator(provider, connectionString, assemblyPath, migrationNamespace, verbose);
		var migrator = migratorResult.Migrator;

		var result = await migrator.MigrateAsync(CancellationToken.None).ConfigureAwait(false);

		if (result.Success)
		{
			if (result.AppliedMigrations.Count == 0)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Out.WriteLine("Database is up to date. No pending migrations.");
				Console.ResetColor();
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Out.WriteLine($"Successfully applied {result.AppliedMigrations.Count} migration(s):");
				Console.ResetColor();

				foreach (var migration in result.AppliedMigrations)
				{
					Console.Out.WriteLine($"  - {migration}");
				}
			}
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine($"Migration failed: {result.ErrorMessage}");
			Console.ResetColor();

			if (result.Exception != null && verbose)
			{
				Console.Error.WriteLine(result.Exception.ToString());
			}

			throw new InvalidOperationException($"Migration failed: {result.ErrorMessage}", result.Exception);
		}
	}
}

