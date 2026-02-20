// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for database schema migration operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core operations for managing database schema migrations:
/// <list type="bullet">
/// <item>Applying pending migrations to bring the database up to date</item>
/// <item>Querying applied migrations for version tracking</item>
/// <item>Rolling back to a specific version when needed</item>
/// </list>
/// </para>
/// <para>
/// Implementations are expected to:
/// <list type="bullet">
/// <item>Use advisory locks or similar mechanisms to prevent concurrent migrations</item>
/// <item>Maintain a migration history table for tracking applied migrations</item>
/// <item>Execute migrations within transactions where supported</item>
/// </list>
/// </para>
/// </remarks>
public interface IMigrator
{
	/// <summary>
	/// Applies all pending migrations to bring the database schema up to date.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the migration operation including applied migrations.</returns>
	/// <remarks>
	/// This method is idempotent - calling it when no migrations are pending will succeed
	/// with an empty list of applied migrations.
	/// </remarks>
	Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets all migrations that have been applied to the database.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of applied migrations in the order they were applied.</returns>
	Task<IReadOnlyList<AppliedMigration>> GetAppliedMigrationsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Rolls back migrations to the specified target version.
	/// </summary>
	/// <param name="targetMigrationId">The migration ID to roll back to (this migration will remain applied).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the rollback operation.</returns>
	/// <remarks>
	/// <para>
	/// The target migration ID specifies the migration to roll back TO, not the migration to remove.
	/// All migrations applied after the target migration will be removed.
	/// </para>
	/// <para>
	/// If the target migration ID is not found in the applied migrations, the operation will fail.
	/// </para>
	/// </remarks>
	Task<MigrationResult> RollbackAsync(string targetMigrationId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all pending migrations that have not yet been applied.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of pending migration IDs in the order they should be applied.</returns>
	Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);
}
