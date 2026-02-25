// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Migration;

/// <summary>
/// Default implementation of <see cref="IMigrationRunner"/> that coordinates event batch migration.
/// </summary>
/// <remarks>
/// <para>
/// This runner uses <see cref="IEventBatchMigrator"/> to execute individual migration plans
/// and supports parallel stream processing via <see cref="MigrationRunnerOptions.ParallelStreams"/>.
/// </para>
/// </remarks>
public sealed partial class MigrationRunner : IMigrationRunner
{
	private readonly IEventBatchMigrator _migrator;
	private readonly ILogger<MigrationRunner> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationRunner"/> class.
	/// </summary>
	/// <param name="migrator">The event batch migrator for executing individual plans.</param>
	/// <param name="logger">The logger.</param>
	public MigrationRunner(
		IEventBatchMigrator migrator,
		ILogger<MigrationRunner> logger)
	{
		_migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<EventMigrationResult> RunAsync(
		MigrationRunnerOptions options,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		LogRunnerStarted(options.DryRun, options.ParallelStreams);

		// Create migration options from runner options
		var migrationOptions = new MigrationOptions
		{
			BatchSize = options.BatchSize,
			DryRun = options.DryRun,
			ContinueOnError = options.ContinueOnError,
		};

		// Create plans
		var plans = await _migrator.CreatePlanAsync(migrationOptions, cancellationToken)
			.ConfigureAwait(false);

		if (plans.Count == 0)
		{
			LogNoPlanFound();

			return new EventMigrationResult(
				EventsMigrated: 0,
				EventsSkipped: 0,
				StreamsMigrated: 0,
				options.DryRun,
				Errors: []);
		}

		LogPlansCreated(plans.Count);

		var totalMigrated = 0L;
		var totalSkipped = 0L;
		var totalStreams = 0;
		var allErrors = new List<string>();

		if (options.ParallelStreams > 1 && plans.Count > 1)
		{
			// Parallel execution
			var parallelOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = options.ParallelStreams,
				CancellationToken = cancellationToken
			};

			await Parallel.ForEachAsync(plans, parallelOptions, async (plan, ct) =>
			{
				var result = await _migrator.MigrateAsync(plan, ct).ConfigureAwait(false);
				Interlocked.Add(ref totalMigrated, result.EventsMigrated);
				Interlocked.Add(ref totalSkipped, result.EventsSkipped);
				Interlocked.Add(ref totalStreams, result.StreamsMigrated);

				lock (allErrors)
				{
					allErrors.AddRange(result.Errors);
				}
			}).ConfigureAwait(false);
		}
		else
		{
			// Sequential execution
			foreach (var plan in plans)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var result = await _migrator.MigrateAsync(plan, cancellationToken)
					.ConfigureAwait(false);

				totalMigrated += result.EventsMigrated;
				totalSkipped += result.EventsSkipped;
				totalStreams += result.StreamsMigrated;
				allErrors.AddRange(result.Errors);

				if (allErrors.Count > 0 && !options.ContinueOnError)
				{
					break;
				}
			}
		}

		LogRunnerCompleted(totalMigrated, totalSkipped, totalStreams);

		return new EventMigrationResult(
			totalMigrated,
			totalSkipped,
			totalStreams,
			options.DryRun,
			allErrors);
	}

	/// <inheritdoc />
	public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
	{
		LogValidationStarted();

		try
		{
			var options = new MigrationOptions();
			var plans = await _migrator.CreatePlanAsync(options, cancellationToken)
				.ConfigureAwait(false);

			// Validate that plans are well-formed
			foreach (var plan in plans)
			{
				if (string.IsNullOrEmpty(plan.SourceStream))
				{
					LogValidationFailed("Source stream is null or empty");
					return false;
				}

				if (string.IsNullOrEmpty(plan.TargetStream))
				{
					LogValidationFailed("Target stream is null or empty");
					return false;
				}

				if (string.Equals(plan.SourceStream, plan.TargetStream, StringComparison.Ordinal))
				{
					LogValidationFailed($"Source and target streams are identical: {plan.SourceStream}");
					return false;
				}
			}

			LogValidationPassed(plans.Count);
			return true;
		}
		catch (Exception ex)
		{
			LogValidationError(ex);
			return false;
		}
	}

	#region Logging

	[LoggerMessage(EventSourcingEventId.MigrationStarted, LogLevel.Information,
		"Migration runner started: DryRun={DryRun}, ParallelStreams={ParallelStreams}")]
	private partial void LogRunnerStarted(bool dryRun, int parallelStreams);

	[LoggerMessage(EventSourcingEventId.MigrationCompleted, LogLevel.Information,
		"Migration runner completed: {EventsMigrated} events migrated, {EventsSkipped} skipped, {StreamsMigrated} streams")]
	private partial void LogRunnerCompleted(long eventsMigrated, long eventsSkipped, int streamsMigrated);

	[LoggerMessage(EventSourcingEventId.MigrationApplied, LogLevel.Information,
		"{PlanCount} migration plans created")]
	private partial void LogPlansCreated(int planCount);

	[LoggerMessage(EventSourcingEventId.NoPendingMigrations, LogLevel.Information,
		"No migration plans found")]
	private partial void LogNoPlanFound();

	[LoggerMessage(EventSourcingEventId.MigrationHistoryCreated, LogLevel.Information,
		"Migration validation started")]
	private partial void LogValidationStarted();

	[LoggerMessage(EventSourcingEventId.PendingMigrationsFound, LogLevel.Information,
		"Migration validation passed: {PlanCount} plans validated")]
	private partial void LogValidationPassed(int planCount);

	[LoggerMessage(EventSourcingEventId.MigrationFailed, LogLevel.Error,
		"Migration validation failed: {Reason}")]
	private partial void LogValidationFailed(string reason);

	[LoggerMessage(EventSourcingEventId.MigrationLockFailed, LogLevel.Error,
		"Migration validation error")]
	private partial void LogValidationError(Exception ex);

	#endregion
}
