// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Configuration options for the legal hold expiration background service.
/// </summary>
public sealed class LegalHoldExpirationOptions
{
	/// <summary>
	/// Gets or sets the interval between polling for expired holds.
	/// Default: 1 hour.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets whether the expiration service is enabled.
	/// Default: true.
	/// </summary>
	public bool Enabled { get; set; } = true;
}

/// <summary>
/// Background service that automatically releases expired legal holds.
/// </summary>
/// <remarks>
/// <para>
/// This service periodically checks for legal holds that have passed their
/// <see cref="LegalHold.ExpiresAt"/> date and releases them via
/// <see cref="ILegalHoldService.ReleaseHoldAsync"/>.
/// </para>
/// <para>
/// Holds without an expiration date are never auto-released and must be
/// explicitly released by an operator.
/// </para>
/// </remarks>
internal sealed partial class LegalHoldExpirationService : BackgroundService
{
	// Process-lifetime instrument on the erasure meter, which a host already subscribes to — a second meter
	// for one counter would be a second name for a consumer to opt into, on the same subsystem.
	//
	// The counter exists BECAUSE the log lines below are not enough. A log line is read by somebody already
	// looking, and a hold whose release keeps losing a concurrency check is precisely the thing nobody is
	// looking at; left unresolved it stays active past its statutory period. This is the signal an operator
	// can alert on, and the outcome tag is what makes a rising conflict share distinguishable from a rising
	// workload without parsing text.
	private static readonly Counter<long> ExpirationOutcomes =
		ErasureTelemetryConstants.Meter.CreateCounter<long>(
			ErasureTelemetryConstants.MetricNames.LegalHoldExpirationOutcomes,
			description: "Expired legal holds the expiration sweep considered, by what it did to each.");

	/// <summary>
	/// Values of the <c>outcome</c> dimension on
	/// <see cref="ErasureTelemetryConstants.MetricNames.LegalHoldExpirationOutcomes"/>. Exhaustive: every
	/// hold the sweep considers is counted exactly once under one of these.
	/// </summary>
	private static class Outcome
	{
		/// <summary>The hold was expired, uncontended, and is now released.</summary>
		public const string Released = "released";

		/// <summary>
		/// The release lost a concurrency check and the hold is no longer expired — an operator extended it.
		/// The hold stays active, which is correct. A steady trickle here is normal operation; a spike means
		/// somebody is extending holds in bulk.
		/// </summary>
		public const string Extended = "extended";

		/// <summary>
		/// The release lost a concurrency check and the hold is still expired and active. It was left for
		/// the next cycle. <b>This is the value to alert on:</b> a hold repeating here is one whose release
		/// never lands, so it outlives the period it was filed for and nothing else says so.
		/// </summary>
		public const string Contended = "contended";

		/// <summary>The sweep's own query returned a hold the store then could not write to.</summary>
		public const string Missing = "missing";

		/// <summary>The release failed for a reason other than a concurrency conflict.</summary>
		public const string Failed = "failed";
	}

	private static void RecordOutcome(string outcome) =>
		ExpirationOutcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<LegalHoldExpirationOptions> _options;
	private readonly ILogger<LegalHoldExpirationService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LegalHoldExpirationService"/> class.
	/// </summary>
	public LegalHoldExpirationService(
		IServiceScopeFactory scopeFactory,
		IOptions<LegalHoldExpirationOptions> options,
		ILogger<LegalHoldExpirationService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Value.Enabled)
		{
			LogExpirationDisabled();
			return;
		}

		LogExpirationStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessExpiredHoldsAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogExpirationProcessingError(ex);
			}

			try
			{
				await Task.Delay(_options.Value.PollingInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogExpirationStopped();
	}

	private async Task ProcessExpiredHoldsAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var holdStore = scope.ServiceProvider.GetRequiredService<ILegalHoldStore>();

		var queryStore = (ILegalHoldQueryStore?)holdStore.GetService(typeof(ILegalHoldQueryStore))
			?? throw new InvalidOperationException("The legal hold store does not support query operations.");

		var expiredHolds = await queryStore.GetExpiredHoldsAsync(cancellationToken).ConfigureAwait(false);

		if (expiredHolds.Count == 0)
		{
			LogNoExpiredHolds();
			return;
		}

		LogProcessingBatch(expiredHolds.Count);

		var conflicts = 0;

		foreach (var hold in expiredHolds)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				// The version travels with the record. A `with` expression over the hold this sweep read
				// carries it across, so the store compares against the state this decision was made on.
				var released = hold with
				{
					IsActive = false,
					ReleasedBy = "System (auto-expiration)",
					ReleasedAt = DateTimeOffset.UtcNow,
					ReleaseReason = $"Hold expired at {hold.ExpiresAt:O}"
				};

				if (await holdStore.UpdateHoldAsync(released, cancellationToken).ConfigureAwait(false))
				{
					RecordOutcome(Outcome.Released);
					LogAutoReleased(hold.HoldId, hold.CaseReference);
				}
				else
				{
					// The store's own query returned this hold moments ago, so an identifier it cannot
					// now write to is worth saying out loud rather than counting as released.
					RecordOutcome(Outcome.Missing);
					LogReleaseTargetMissing(hold.HoldId, hold.CaseReference);
				}
			}
			catch (LegalHoldConcurrencyException)
			{
				conflicts++;

				// The outcome tag is recorded inside, once the re-read says which conflict this was.
				await ReportConflictAsync(holdStore, hold, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				RecordOutcome(Outcome.Failed);
				LogReleaseFailed(hold.HoldId, ex);
			}
		}

		if (conflicts > 0)
		{
			LogCycleConflicts(conflicts, expiredHolds.Count);
		}
	}

	/// <summary>
	/// Re-reads a hold whose release lost a concurrency check and reports what the new state means.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Re-read and re-evaluate; never retry.</b> The conflict says the record moved, and the reason it
	/// most often moved is the one this sweep must not override: an operator extending the expiry because
	/// the statutory period was longer than first recorded, or because a new obligation attached. Re-applying
	/// the release against a fresh version would reinstate exactly the lost update the concurrency token
	/// exists to prevent, with extra steps.
	/// </para>
	/// <para>
	/// So nothing is written here. A hold that is genuinely still expired is simply left for the next cycle,
	/// which errs toward keeping a hold that should have lapsed -- recoverable, an operator releases it --
	/// rather than lifting one that should have stood, which destroys records irreversibly.
	/// </para>
	/// </remarks>
	private async Task ReportConflictAsync(
		ILegalHoldStore holdStore,
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		LegalHold? current;

		try
		{
			current = await holdStore.GetHoldAsync(hold.HoldId, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// The conflict itself is still reported below via the cycle count; only the re-read failed.
			// Counted as contended rather than failed: the write WAS refused by a concurrency check, and
			// undercounting the value an operator alerts on is the wrong direction to err in.
			RecordOutcome(Outcome.Contended);
			LogReleaseFailed(hold.HoldId, ex);
			return;
		}

		if (current is null || !current.IsActive)
		{
			// Somebody else released it, which is the outcome this sweep wanted.
			RecordOutcome(Outcome.Released);
			return;
		}

		var now = DateTimeOffset.UtcNow;

		if (current.ExpiresAt is null || current.ExpiresAt > now)
		{
			RecordOutcome(Outcome.Extended);
			LogConflictHoldExtended(hold.HoldId, hold.CaseReference, current.ExpiresAt);
			return;
		}

		RecordOutcome(Outcome.Contended);
		LogConflictUnresolved(hold.HoldId, hold.CaseReference);
	}

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationDisabled,
		LogLevel.Information,
		"Legal hold expiration background service is disabled")]
	private partial void LogExpirationDisabled();

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationStarting,
		LogLevel.Information,
		"Legal hold expiration background service starting with polling interval {PollingInterval}")]
	private partial void LogExpirationStarting(TimeSpan pollingInterval);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationStopped,
		LogLevel.Information,
		"Legal hold expiration background service stopped")]
	private partial void LogExpirationStopped();

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationProcessingError,
		LogLevel.Error,
		"Error in legal hold expiration processing cycle")]
	private partial void LogExpirationProcessingError(Exception exception);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationNoExpiredHolds,
		LogLevel.Debug,
		"No expired legal holds found")]
	private partial void LogNoExpiredHolds();

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationProcessingBatch,
		LogLevel.Information,
		"Processing {Count} expired legal holds")]
	private partial void LogProcessingBatch(int count);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationAutoReleased,
		LogLevel.Information,
		"Auto-released expired legal hold {HoldId} for case {CaseReference}")]
	private partial void LogAutoReleased(Guid holdId, string caseReference);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationReleaseFailed,
		LogLevel.Error,
		"Failed to auto-release expired legal hold {HoldId}")]
	private partial void LogReleaseFailed(Guid holdId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationTargetMissing,
		LogLevel.Warning,
		"Expired legal hold {HoldId} for case {CaseReference} could not be released: no such hold is "
		+ "visible to this store. It was deleted or is owned by another tenant. Nothing was released.")]
	private partial void LogReleaseTargetMissing(Guid holdId, string caseReference);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationConflictHoldExtended,
		LogLevel.Warning,
		"Legal hold {HoldId} for case {CaseReference} was modified while the expiration sweep was "
		+ "releasing it, and now expires at {ExpiresAt}. The hold REMAINS ACTIVE and was not released.")]
	private partial void LogConflictHoldExtended(Guid holdId, string caseReference, DateTimeOffset? expiresAt);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationConflictUnresolved,
		LogLevel.Warning,
		"Legal hold {HoldId} for case {CaseReference} was modified while the expiration sweep was "
		+ "releasing it, and is still expired and active. It was left for the next cycle. Repeated "
		+ "reports for the same hold mean a writer is contending with the sweep.")]
	private partial void LogConflictUnresolved(Guid holdId, string caseReference);

	[LoggerMessage(
		ComplianceEventId.LegalHoldExpirationCycleConflicts,
		LogLevel.Warning,
		"{ConflictCount} of {BatchCount} expired legal holds could not be released this cycle because "
		+ "they were modified concurrently. Nothing was overwritten.")]
	private partial void LogCycleConflicts(int conflictCount, int batchCount);
}
