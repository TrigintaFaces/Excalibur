// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Migrate.Tool;

/// <summary>
/// Result containing the migrator and associated resources.
/// </summary>
/// <remarks>
/// Dispose this result to clean up the logger factory.
/// </remarks>
internal sealed class MigratorResult : IDisposable
{
	private readonly ILoggerFactory? _loggerFactory;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MigratorResult"/> class.
	/// </summary>
	/// <param name="migrator">The migrator instance.</param>
	/// <param name="loggerFactory">The logger factory (null if using NullLoggerFactory).</param>
	public MigratorResult(IMigrator migrator, ILoggerFactory? loggerFactory)
	{
		Migrator = migrator;
		_loggerFactory = loggerFactory;
	}

	/// <summary>
	/// Gets the migrator instance.
	/// </summary>
	public IMigrator Migrator { get; }

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		(_loggerFactory as IDisposable)?.Dispose();
	}
}

/// <summary>
/// Factory for creating database-specific migrator instances.
/// </summary>
internal static class MigratorFactory
{
	/// <summary>
	/// Creates a migrator for the specified provider.
	/// </summary>
	/// <param name="provider">The database provider (sqlserver or postgres).</param>
	/// <param name="connectionString">The database connection string.</param>
	/// <param name="assemblyPath">Optional path to the assembly containing migrations.</param>
	/// <param name="migrationNamespace">Optional namespace prefix for migration resources.</param>
	/// <param name="verbose">Whether to enable verbose logging.</param>
	/// <returns>The configured migrator result (dispose when done).</returns>
	/// <exception cref="ArgumentException">Thrown when the provider is not supported.</exception>
	public static MigratorResult CreateMigrator(
		string provider,
		string connectionString,
		string? assemblyPath,
		string? migrationNamespace,
		bool verbose)
	{
		var assembly = LoadMigrationAssembly(assemblyPath);
		var ns = migrationNamespace ?? GetDefaultNamespace(assembly);
		var (loggerFactory, disposeLoggerFactory) = CreateLoggerFactory(verbose);

		var migrator = provider.ToUpperInvariant() switch
		{
			"SQLSERVER" => (IMigrator)new SqlServerMigrator(connectionString, assembly, ns, loggerFactory.CreateLogger<SqlServerMigrator>()),
			"POSTGRES" or "Postgres" => new PostgresMigrator(connectionString, assembly, ns, loggerFactory.CreateLogger<PostgresMigrator>()),
			_ => throw new ArgumentException($"Unsupported database provider: {provider}. Supported providers: sqlserver, postgres", nameof(provider)),
		};

		return new MigratorResult(migrator, disposeLoggerFactory ? loggerFactory : null);
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
		// Use the assembly name as the default namespace
		return assembly.GetName().Name ?? "Migrations";
	}

	private static (ILoggerFactory Factory, bool ShouldDispose) CreateLoggerFactory(bool verbose)
	{
		if (!verbose)
		{
			return (NullLoggerFactory.Instance, false);
		}

		var factory = LoggerFactory.Create(builder =>
		{
			_ = builder.AddConsole();
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});

		return (factory, true);
	}
}
