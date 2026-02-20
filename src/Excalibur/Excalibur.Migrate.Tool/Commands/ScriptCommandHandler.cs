// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;
using System.Text;

namespace Excalibur.Migrate.Tool.Commands;

/// <summary>
/// Handler for the 'script' command that generates SQL scripts for pending migrations.
/// </summary>
internal sealed class ScriptCommandHandler
{
	/// <summary>
	/// Executes the script command to generate SQL script for pending migrations.
	/// </summary>
	/// <param name="provider">Database provider.</param>
	/// <param name="connectionString">Connection string.</param>
	/// <param name="assemblyPath">Optional assembly path.</param>
	/// <param name="migrationNamespace">Optional migration namespace.</param>
	/// <param name="verbose">Whether to enable verbose output.</param>
	/// <param name="outputPath">Output file path for the script.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		string provider,
		string connectionString,
		string? assemblyPath,
		string? migrationNamespace,
		bool verbose,
		string outputPath)
	{
		Console.WriteLine($"Generating migration script for {provider}...");

		using var migratorResult = MigratorFactory.CreateMigrator(provider, connectionString, assemblyPath, migrationNamespace, verbose);
		var migrator = migratorResult.Migrator;

		var pendingMigrations = await migrator.GetPendingMigrationsAsync(CancellationToken.None).ConfigureAwait(false);

		if (pendingMigrations.Count == 0)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("No pending migrations. Nothing to script.");
			Console.ResetColor();
			return;
		}

		var assembly = LoadMigrationAssembly(assemblyPath);
		var ns = migrationNamespace ?? GetDefaultNamespace(assembly);

		var scriptBuilder = new StringBuilder();
		_ = scriptBuilder.AppendLine("-- ================================================================================");
		_ = scriptBuilder.AppendLine($"-- Excalibur Migration Script");
		_ = scriptBuilder.AppendLine($"-- Provider: {provider}");
		_ = scriptBuilder.AppendLine($"-- Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
		_ = scriptBuilder.AppendLine($"-- Pending Migrations: {pendingMigrations.Count}");
		_ = scriptBuilder.AppendLine("-- ================================================================================");
		_ = scriptBuilder.AppendLine();

		foreach (var migrationId in pendingMigrations)
		{
			var resourceName = $"{ns}.{migrationId}.sql";
			var script = LoadEmbeddedResource(assembly, resourceName, ns);

			if (script == null)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine($"Warning: Could not find migration script for '{migrationId}'");
				Console.ResetColor();
				continue;
			}

			_ = scriptBuilder.AppendLine($"-- Migration: {migrationId}");
			_ = scriptBuilder.AppendLine("-- " + new string('-', 78));
			_ = scriptBuilder.AppendLine();
			_ = scriptBuilder.AppendLine(script);
			_ = scriptBuilder.AppendLine();
			_ = scriptBuilder.AppendLine($"-- End of migration: {migrationId}");
			_ = scriptBuilder.AppendLine();
		}

		var fullPath = Path.GetFullPath(outputPath);
		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			_ = Directory.CreateDirectory(directory);
		}

		await File.WriteAllTextAsync(fullPath, scriptBuilder.ToString()).ConfigureAwait(false);

		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"Successfully generated migration script with {pendingMigrations.Count} migration(s).");
		Console.WriteLine($"Output: {fullPath}");
		Console.ResetColor();
	}

	private static Assembly LoadMigrationAssembly(string? assemblyPath)
	{
		if (string.IsNullOrWhiteSpace(assemblyPath))
		{
			return Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
		}

		var fullPath = Path.GetFullPath(assemblyPath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"Migration assembly not found: {fullPath}");
		}

		return Assembly.LoadFrom(fullPath);
	}

	private static string GetDefaultNamespace(Assembly assembly)
	{
		return assembly.GetName().Name ?? "Migrations";
	}

	private static string? LoadEmbeddedResource(Assembly assembly, string resourceName, string namespacePrefix)
	{
		// Try exact match first
		using var stream = assembly.GetManifestResourceStream(resourceName);
		if (stream != null)
		{
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		// Try to find by suffix matching
		var resourceNames = assembly.GetManifestResourceNames();
		var migrationId = resourceName.Replace(namespacePrefix + ".", string.Empty, StringComparison.Ordinal);

		foreach (var name in resourceNames)
		{
			if (name.EndsWith(migrationId, StringComparison.OrdinalIgnoreCase))
			{
				using var altStream = assembly.GetManifestResourceStream(name);
				if (altStream != null)
				{
					using var reader = new StreamReader(altStream);
					return reader.ReadToEnd();
				}
			}
		}

		return null;
	}
}
