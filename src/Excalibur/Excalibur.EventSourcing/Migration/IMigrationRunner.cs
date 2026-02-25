// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Runs event sourcing migrations, coordinating the execution of migration plans.
/// </summary>
/// <remarks>
/// <para>
/// The migration runner discovers and executes migration plans, supporting:
/// <list type="bullet">
/// <item>Dry-run validation without applying changes</item>
/// <item>Parallel stream processing for throughput</item>
/// <item>Error handling with continue-on-error support</item>
/// <item>Pre-run validation of migration plans</item>
/// </list>
/// </para>
/// <para>
/// Register via <c>services.AddEventSourcingMigration(opts => ...)</c> in the DI container.
/// </para>
/// </remarks>
public interface IMigrationRunner
{
	/// <summary>
	/// Runs all pending migrations according to the configured options.
	/// </summary>
	/// <param name="options">The migration runner options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous migration operation.</returns>
	Task<EventMigrationResult> RunAsync(MigrationRunnerOptions options, CancellationToken cancellationToken);

	/// <summary>
	/// Validates the migration configuration and plans without executing them.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if validation passed; otherwise, <see langword="false"/>.</returns>
	Task<bool> ValidateAsync(CancellationToken cancellationToken);
}
