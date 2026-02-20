// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Provides event batch migration capabilities for migrating events between streams.
/// </summary>
/// <remarks>
/// <para>
/// The event batch migrator enables bulk event migration scenarios such as:
/// <list type="bullet">
/// <item>Stream renaming or restructuring</item>
/// <item>Event schema migration with transformation</item>
/// <item>Selective event copying with filters</item>
/// <item>Dry-run validation of migration plans</item>
/// </list>
/// </para>
/// <para>
/// Use <see cref="CreatePlanAsync"/> to build a migration plan from options,
/// then execute it with <see cref="MigrateAsync"/>. The dry-run mode in
/// <see cref="MigrationOptions"/> allows validating without writing.
/// </para>
/// </remarks>
public interface IEventBatchMigrator
{
	/// <summary>
	/// Executes a migration plan, reading events from the source stream,
	/// applying optional filters and transforms, and writing to the target stream.
	/// </summary>
	/// <param name="plan">The migration plan to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of the migration operation.</returns>
	Task<EventMigrationResult> MigrateAsync(MigrationPlan plan, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a migration plan from the specified options.
	/// </summary>
	/// <param name="options">The migration configuration options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A list of migration plans, one per matching source stream.</returns>
	Task<IReadOnlyList<MigrationPlan>> CreatePlanAsync(MigrationOptions options, CancellationToken cancellationToken);
}
