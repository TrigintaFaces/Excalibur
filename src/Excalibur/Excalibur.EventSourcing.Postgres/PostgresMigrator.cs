// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Dapper;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Postgres implementation of <see cref="IMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// This migrator executes SQL scripts embedded as resources in the migration assembly.
/// Scripts should be named following the pattern: YYYYMMDDHHMMSS_MigrationName.sql
/// </para>
/// <para>
/// Thread safety is ensured using Postgres advisory locks (pg_advisory_lock/pg_advisory_unlock)
/// to prevent concurrent migrations during multi-instance startup.
/// </para>
/// <para>
/// Uses snake_case naming convention per ADR-109 for Postgres compatibility.
/// </para>
/// </remarks>
public sealed partial class PostgresMigrator : IMigrator
{
	private const string MigrationHistoryTableName = "migration_history";
	private const long AdvisoryLockId = 0x45584D4947524154; // "EXMIGRAT" in hex

	private readonly NpgsqlDataSource _dataSource;
	private readonly Assembly _migrationAssembly;
	private readonly string _migrationNamespace;
	private readonly ILogger<PostgresMigrator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresMigrator"/> class.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresMigrator(
		string connectionString,
		Assembly migrationAssembly,
		string migrationNamespace,
		ILogger<PostgresMigrator> logger)
		: this(NpgsqlDataSource.Create(connectionString), migrationAssembly, migrationNamespace, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresMigrator"/> class.
	/// </summary>
	/// <param name="dataSource">The NpgsqlDataSource for connection pooling.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <param name="logger">The logger instance.</param>
	public PostgresMigrator(
		NpgsqlDataSource dataSource,
		Assembly migrationAssembly,
		string migrationNamespace,
		ILogger<PostgresMigrator> logger)
	{
		_dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
		_migrationAssembly = migrationAssembly ?? throw new ArgumentNullException(nameof(migrationAssembly));
		_migrationNamespace = migrationNamespace ?? throw new ArgumentNullException(nameof(migrationNamespace));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		LogMigratorCreated();
	}

	/// <inheritdoc />
	public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken)
	{
		LogMigrationStarted();

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		// Acquire advisory lock to prevent concurrent migrations
		if (!await TryAcquireLockAsync(connection, cancellationToken).ConfigureAwait(false))
		{
			LogMigrationLockFailed();
			return MigrationResult.Failed("Failed to acquire migration lock. Another migration may be in progress.");
		}

		try
		{
			// Ensure migration history table exists
			await EnsureMigrationHistoryTableAsync(connection, cancellationToken).ConfigureAwait(false);

			// Get pending migrations
			var pendingMigrations = await GetPendingMigrationsInternalAsync(cancellationToken).ConfigureAwait(false);

			if (pendingMigrations.Count == 0)
			{
				LogNoPendingMigrations();
				return MigrationResult.NoMigrationsPending();
			}

			LogPendingMigrationsFound(pendingMigrations.Count);

			var appliedMigrations = new List<AppliedMigration>();

			foreach (var migration in pendingMigrations)
			{
				try
				{
					var appliedMigration = await ApplyMigrationAsync(connection, migration, cancellationToken).ConfigureAwait(false);
					appliedMigrations.Add(appliedMigration);
					LogMigrationApplied(migration.Id);
				}
				catch (Exception ex)
				{
					LogMigrationFailed(ex, migration.Id);
					return MigrationResult.Failed($"Migration {migration.Id} failed: {ex.Message}", ex);
				}
			}

			LogMigrationCompleted(appliedMigrations.Count);
			return MigrationResult.Succeeded(appliedMigrations);
		}
		finally
		{
			await ReleaseLockAsync(connection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<AppliedMigration>> GetAppliedMigrationsAsync(CancellationToken cancellationToken)
	{
		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		// Check if history table exists
		var tableExists = await connection.ExecuteScalarAsync<bool>(
			new CommandDefinition(
				"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @TableName)",
				new { TableName = MigrationHistoryTableName },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (!tableExists)
		{
			return [];
		}

		var sql = $"""
			SELECT migration_id AS MigrationId, applied_at AS AppliedAt, description AS Description, checksum AS Checksum
			FROM {MigrationHistoryTableName}
			ORDER BY applied_at
			""";

		var migrations = await connection.QueryAsync<MigrationHistoryRow>(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

		return migrations.Select(m => new AppliedMigration
		{
			MigrationId = m.MigrationId,
			AppliedAt = m.AppliedAt,
			Description = m.Description,
			Checksum = m.Checksum
		}).ToList();
	}

	/// <inheritdoc />
	public async Task<MigrationResult> RollbackAsync(string targetMigrationId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(targetMigrationId);

		LogRollbackStarted(targetMigrationId);

		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		if (!await TryAcquireLockAsync(connection, cancellationToken).ConfigureAwait(false))
		{
			LogMigrationLockFailed();
			return MigrationResult.Failed("Failed to acquire migration lock. Another migration may be in progress.");
		}

		try
		{
			var appliedMigrations = await GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
			var targetIndex = appliedMigrations.ToList().FindIndex(m => m.MigrationId == targetMigrationId);

			if (targetIndex < 0)
			{
				return MigrationResult.Failed($"Target migration '{targetMigrationId}' not found in applied migrations.");
			}

			// Remove migrations after the target (in reverse order)
			var migrationsToRemove = appliedMigrations.Skip(targetIndex + 1).Reverse().ToList();

			if (migrationsToRemove.Count == 0)
			{
				return MigrationResult.NoMigrationsPending();
			}

			foreach (var migration in migrationsToRemove)
			{
				await RemoveMigrationRecordAsync(connection, migration.MigrationId, cancellationToken).ConfigureAwait(false);
			}

			LogRollbackCompleted(migrationsToRemove.Count);
			return MigrationResult.Succeeded(migrationsToRemove);
		}
		catch (Exception ex)
		{
			LogRollbackFailed(ex);
			return MigrationResult.Failed($"Rollback failed: {ex.Message}", ex);
		}
		finally
		{
			await ReleaseLockAsync(connection, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
	{
		var pending = await GetPendingMigrationsInternalAsync(cancellationToken).ConfigureAwait(false);
		return pending.Select(m => m.Id).ToList();
	}

	private async Task<bool> TryAcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		// pg_try_advisory_lock returns true if the lock was acquired
		var result = await connection.ExecuteScalarAsync<bool>(
			new CommandDefinition(
				"SELECT pg_try_advisory_lock(@LockId)",
				new { LockId = AdvisoryLockId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (result)
		{
			LogMigrationLockAcquired();
			return true;
		}

		return false;
	}

	private async Task ReleaseLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		try
		{
			await connection.ExecuteAsync(
				new CommandDefinition(
					"SELECT pg_advisory_unlock(@LockId)",
					new { LockId = AdvisoryLockId },
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			LogMigrationLockReleased();
		}
		catch
		{
			// Lock release failures are not critical - the lock will expire when the session ends
		}
	}

	private async Task EnsureMigrationHistoryTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var sql = $"""
			CREATE TABLE IF NOT EXISTS {MigrationHistoryTableName} (
				migration_id VARCHAR(150) NOT NULL PRIMARY KEY,
				applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
				description VARCHAR(500) NULL,
				checksum VARCHAR(64) NULL
			)
			""";

		await connection.ExecuteAsync(
			new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

		LogMigrationHistoryCreated();
	}

	private async Task<List<PendingMigration>> GetPendingMigrationsInternalAsync(CancellationToken cancellationToken)
	{
		// Get all migration scripts from the assembly
		var allMigrations = GetMigrationScriptsFromAssembly();

		// Get applied migrations (uses its own connection)
		var appliedMigrations = await GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
		var appliedIds = appliedMigrations.Select(m => m.MigrationId).ToHashSet(StringComparer.OrdinalIgnoreCase);

		// Return pending migrations in order
		return allMigrations
			.Where(m => !appliedIds.Contains(m.Id))
			.OrderBy(m => m.Id)
			.ToList();
	}

	private List<PendingMigration> GetMigrationScriptsFromAssembly()
	{
		var prefix = _migrationNamespace + ".";
		var suffix = ".sql";

		return _migrationAssembly
			.GetManifestResourceNames()
			.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
						   name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			.Select(name =>
			{
				var migrationId = name[prefix.Length..^suffix.Length];
				return new PendingMigration(migrationId, name);
			})
			.OrderBy(m => m.Id)
			.ToList();
	}

	private async Task<AppliedMigration> ApplyMigrationAsync(NpgsqlConnection connection, PendingMigration migration, CancellationToken cancellationToken)
	{
		// Read the migration script
		using var stream = _migrationAssembly.GetManifestResourceStream(migration.ResourceName)
			?? throw new InvalidOperationException($"Migration resource not found: {migration.ResourceName}");

		using var reader = new StreamReader(stream, Encoding.UTF8);
		var script = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

		// Calculate checksum
		var checksum = ComputeChecksum(script);

		// Execute the migration within a transaction
		await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			// Execute migration script
			await connection.ExecuteAsync(
				new CommandDefinition(
					script,
					transaction: transaction,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			// Record the migration
			var insertSql = $"""
				INSERT INTO {MigrationHistoryTableName} (migration_id, applied_at, description, checksum)
				VALUES (@MigrationId, @AppliedAt, @Description, @Checksum)
				""";

			var appliedAt = DateTimeOffset.UtcNow;
			await connection.ExecuteAsync(
				new CommandDefinition(
					insertSql,
					new
					{
						MigrationId = migration.Id,
						AppliedAt = appliedAt,
						Description = ExtractDescription(migration.Id),
						Checksum = checksum
					},
					transaction: transaction,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

			return new AppliedMigration
			{
				MigrationId = migration.Id,
				AppliedAt = appliedAt,
				Description = ExtractDescription(migration.Id),
				Checksum = checksum
			};
		}
		catch
		{
			await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
			throw;
		}
	}

	private async Task RemoveMigrationRecordAsync(NpgsqlConnection connection, string migrationId, CancellationToken cancellationToken)
	{
		var sql = $"DELETE FROM {MigrationHistoryTableName} WHERE migration_id = @MigrationId";
		await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { MigrationId = migrationId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	private static string ComputeChecksum(string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		var hash = SHA256.HashData(bytes);
		return Convert.ToHexString(hash);
	}

	private static string? ExtractDescription(string migrationId)
	{
		// Migration IDs are typically in format: 20260205120000_CreateEventsTable
		var underscoreIndex = migrationId.IndexOf('_', StringComparison.Ordinal);
		return underscoreIndex > 0 ? migrationId[(underscoreIndex + 1)..].Replace("_", " ", StringComparison.Ordinal) : null;
	}

	// Logging methods
	[LoggerMessage(EventSourcingEventId.MigratorCreated, LogLevel.Debug, "Postgres migrator created")]
	private partial void LogMigratorCreated();

	[LoggerMessage(EventSourcingEventId.MigrationStarted, LogLevel.Information, "Starting database migration")]
	private partial void LogMigrationStarted();

	[LoggerMessage(EventSourcingEventId.MigrationCompleted, LogLevel.Information, "Database migration completed: {Count} migrations applied")]
	private partial void LogMigrationCompleted(int count);

	[LoggerMessage(EventSourcingEventId.MigrationFailed, LogLevel.Error, "Migration {MigrationId} failed")]
	private partial void LogMigrationFailed(Exception ex, string migrationId);

	[LoggerMessage(EventSourcingEventId.MigrationApplied, LogLevel.Information, "Applied migration: {MigrationId}")]
	private partial void LogMigrationApplied(string migrationId);

	[LoggerMessage(EventSourcingEventId.RollbackStarted, LogLevel.Information, "Starting rollback to migration: {TargetMigrationId}")]
	private partial void LogRollbackStarted(string targetMigrationId);

	[LoggerMessage(EventSourcingEventId.RollbackCompleted, LogLevel.Information, "Rollback completed: {Count} migrations removed")]
	private partial void LogRollbackCompleted(int count);

	[LoggerMessage(EventSourcingEventId.RollbackFailed, LogLevel.Error, "Rollback failed")]
	private partial void LogRollbackFailed(Exception ex);

	[LoggerMessage(EventSourcingEventId.MigrationLockAcquired, LogLevel.Debug, "Migration advisory lock acquired")]
	private partial void LogMigrationLockAcquired();

	[LoggerMessage(EventSourcingEventId.MigrationLockReleased, LogLevel.Debug, "Migration advisory lock released")]
	private partial void LogMigrationLockReleased();

	[LoggerMessage(EventSourcingEventId.MigrationLockFailed, LogLevel.Warning, "Failed to acquire migration lock")]
	private partial void LogMigrationLockFailed();

	[LoggerMessage(EventSourcingEventId.MigrationHistoryCreated, LogLevel.Debug, "Migration history table ensured")]
	private partial void LogMigrationHistoryCreated();

	[LoggerMessage(EventSourcingEventId.PendingMigrationsFound, LogLevel.Information, "Found {Count} pending migrations")]
	private partial void LogPendingMigrationsFound(int count);

	[LoggerMessage(EventSourcingEventId.NoPendingMigrations, LogLevel.Debug, "No pending migrations found")]
	private partial void LogNoPendingMigrations();

	private sealed record PendingMigration(string Id, string ResourceName);

	private sealed class MigrationHistoryRow
	{
		public string MigrationId { get; init; } = string.Empty;
		public DateTimeOffset AppliedAt { get; init; }
		public string? Description { get; init; }
		public string? Checksum { get; init; }
	}
}
