// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Dapper;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>
/// SQL Server implementation of <see cref="IMigrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// This migrator executes SQL scripts embedded as resources in the migration assembly.
/// Scripts should be named following the pattern: YYYYMMDDHHMMSS_MigrationName.sql
/// </para>
/// <para>
/// Thread safety is ensured using SQL Server application locks (sp_getapplock/sp_releaseapplock)
/// to prevent concurrent migrations during multi-instance startup.
/// </para>
/// </remarks>
public sealed partial class SqlServerMigrator : IMigrator
{
	private const string MigrationHistoryTableName = "__MigrationHistory";
	private const string LockResourceName = "Excalibur_Migration_Lock";
	private const int LockTimeoutMs = 30000; // 30 seconds

	private readonly Func<SqlConnection> _connectionFactory;
	private readonly Assembly _migrationAssembly;
	private readonly string _migrationNamespace;
	private readonly ILogger<SqlServerMigrator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMigrator"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerMigrator(
		string connectionString,
		Assembly migrationAssembly,
		string migrationNamespace,
		ILogger<SqlServerMigrator> logger)
		: this(() => new SqlConnection(connectionString), migrationAssembly, migrationNamespace, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerMigrator"/> class.
	/// </summary>
	/// <param name="connectionFactory">Factory function to create SQL connections.</param>
	/// <param name="migrationAssembly">The assembly containing migration scripts as embedded resources.</param>
	/// <param name="migrationNamespace">The namespace prefix for migration resources (e.g., "MyApp.Migrations").</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerMigrator(
		Func<SqlConnection> connectionFactory,
		Assembly migrationAssembly,
		string migrationNamespace,
		ILogger<SqlServerMigrator> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_migrationAssembly = migrationAssembly ?? throw new ArgumentNullException(nameof(migrationAssembly));
		_migrationNamespace = migrationNamespace ?? throw new ArgumentNullException(nameof(migrationNamespace));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		LogMigratorCreated();
	}

	/// <inheritdoc />
	public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken)
	{
		LogMigrationStarted();

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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
			var pendingMigrations = await GetPendingMigrationsInternalAsync(connection, cancellationToken).ConfigureAwait(false);

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
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		// Check if history table exists
		var tableExists = await connection.ExecuteScalarAsync<int>(
			new CommandDefinition(
				"SELECT CASE WHEN OBJECT_ID(@TableName, 'U') IS NOT NULL THEN 1 ELSE 0 END",
				new { TableName = MigrationHistoryTableName },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (tableExists == 0)
		{
			return [];
		}

		var sql = $"""
		           SELECT MigrationId, AppliedAt, Description, Checksum
		           FROM [{MigrationHistoryTableName}]
		           ORDER BY AppliedAt
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

		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

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
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		var pending = await GetPendingMigrationsInternalAsync(connection, cancellationToken).ConfigureAwait(false);
		return pending.Select(m => m.Id).ToList();
	}

	private static string ComputeChecksum(string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		var hash = SHA256.HashData(bytes);
		return Convert.ToHexString(hash);
	}

	private static string[] SplitIntoBatches(string script) =>
		script.Split(["\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n"], StringSplitOptions.RemoveEmptyEntries);

	private static string? ExtractDescription(string migrationId)
	{
		// Migration IDs are typically in format: 20260205120000_CreateEventsTable
		var underscoreIndex = migrationId.IndexOf('_', StringComparison.Ordinal);
		return underscoreIndex > 0 ? migrationId[(underscoreIndex + 1)..].Replace("_", " ", StringComparison.Ordinal) : null;
	}

	private async Task<bool> TryAcquireLockAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		var sql = """
		          DECLARE @result INT;
		          EXEC @result = sp_getapplock @Resource = @ResourceName, @LockMode = 'Exclusive', @LockTimeout = @Timeout;
		          SELECT @result;
		          """;

		var result = await connection.ExecuteScalarAsync<int>(
			new CommandDefinition(
				sql,
				new { ResourceName = LockResourceName, Timeout = LockTimeoutMs },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (result >= 0)
		{
			LogMigrationLockAcquired();
			return true;
		}

		return false;
	}

	private async Task ReleaseLockAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		try
		{
			var sql = "EXEC sp_releaseapplock @Resource = @ResourceName;";
			await connection.ExecuteAsync(
				new CommandDefinition(
					sql,
					new { ResourceName = LockResourceName },
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			LogMigrationLockReleased();
		}
		catch
		{
			// Lock release failures are not critical - the lock will expire when the connection closes
		}
	}

	private async Task EnsureMigrationHistoryTableAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		var sql = $"""
		           IF OBJECT_ID(@TableName, 'U') IS NULL
		           BEGIN
		           	CREATE TABLE [{MigrationHistoryTableName}] (
		           		[MigrationId] NVARCHAR(150) NOT NULL PRIMARY KEY,
		           		[AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
		           		[Description] NVARCHAR(500) NULL,
		           		[Checksum] NVARCHAR(64) NULL
		           	);
		           END
		           """;

		await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { TableName = MigrationHistoryTableName },
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		LogMigrationHistoryCreated();
	}

	private async Task<List<PendingMigration>> GetPendingMigrationsInternalAsync(SqlConnection _, CancellationToken cancellationToken)
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

	private async Task<AppliedMigration> ApplyMigrationAsync(SqlConnection connection, PendingMigration migration,
		CancellationToken cancellationToken)
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
			// Execute migration script (may contain multiple batches separated by GO)
			var batches = SplitIntoBatches(script);
			foreach (var batch in batches)
			{
				if (!string.IsNullOrWhiteSpace(batch))
				{
					await connection.ExecuteAsync(
						new CommandDefinition(
							batch,
							transaction: transaction,
							cancellationToken: cancellationToken)).ConfigureAwait(false);
				}
			}

			// Record the migration
			var insertSql = $"""
			                 INSERT INTO [{MigrationHistoryTableName}] (MigrationId, AppliedAt, Description, Checksum)
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

	private async Task RemoveMigrationRecordAsync(SqlConnection connection, string migrationId, CancellationToken cancellationToken)
	{
		var sql = $"DELETE FROM [{MigrationHistoryTableName}] WHERE MigrationId = @MigrationId";
		await connection.ExecuteAsync(
			new CommandDefinition(
				sql,
				new { MigrationId = migrationId },
				cancellationToken: cancellationToken)).ConfigureAwait(false);
	}

	// Logging methods
	[LoggerMessage(EventSourcingEventId.MigratorCreated, LogLevel.Debug, "SQL Server migrator created")]
	private partial void LogMigratorCreated();

	[LoggerMessage(EventSourcingEventId.MigrationStarted, LogLevel.Information, "Starting database migration")]
	private partial void LogMigrationStarted();

	[LoggerMessage(EventSourcingEventId.MigrationCompleted, LogLevel.Information,
		"Database migration completed: {Count} migrations applied")]
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
