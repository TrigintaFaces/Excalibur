// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Hosted service that runs SQL Server schema migrations on application startup.
/// </summary>
/// <remarks>
/// <para>
/// This service is automatically registered when <see cref="SqlServerMigratorOptions.AutoMigrateOnStartup"/>
/// is set to <see langword="true"/>.
/// </para>
/// <para>
/// Migrations are run during the <see cref="StartAsync"/> phase, blocking application startup
/// until all migrations are applied. This ensures the database schema is up-to-date before
/// the application starts processing requests.
/// </para>
/// </remarks>
/// <param name="migrator">The migrator instance.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class SqlServerMigrationHostedService(
	IMigrator migrator,
	ILogger<SqlServerMigrationHostedService> logger) : IHostedService
{
	private readonly IMigrator _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
	private readonly ILogger<SqlServerMigrationHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		LogStartingMigration();

		try
		{
			var result = await _migrator.MigrateAsync(cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				if (result.AppliedMigrations.Count > 0)
				{
					LogMigrationSucceeded(result.AppliedMigrations.Count);
				}
				else
				{
					LogNoMigrationsPending();
				}
			}
			else
			{
				LogMigrationFailed(result.ErrorMessage ?? "Unknown error");
				throw new InvalidOperationException($"Database migration failed: {result.ErrorMessage}", result.Exception);
			}
		}
		catch (Exception ex) when (ex is not InvalidOperationException)
		{
			LogMigrationException(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(1, LogLevel.Information, "Starting database migration on application startup")]
	private partial void LogStartingMigration();

	[LoggerMessage(2, LogLevel.Information, "Database migration completed successfully: {Count} migrations applied")]
	private partial void LogMigrationSucceeded(int count);

	[LoggerMessage(3, LogLevel.Debug, "No pending migrations found")]
	private partial void LogNoMigrationsPending();

	[LoggerMessage(4, LogLevel.Error, "Database migration failed: {ErrorMessage}")]
	private partial void LogMigrationFailed(string errorMessage);

	[LoggerMessage(5, LogLevel.Error, "Database migration threw an exception")]
	private partial void LogMigrationException(Exception ex);
}
