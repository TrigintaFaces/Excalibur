// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for the legal hold expiration background service.
/// </summary>
public class LegalHoldExpirationOptions
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
public partial class LegalHoldExpirationService : BackgroundService
{
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

		foreach (var hold in expiredHolds)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				var released = hold with
				{
					IsActive = false,
					ReleasedBy = "System (auto-expiration)",
					ReleasedAt = DateTimeOffset.UtcNow,
					ReleaseReason = $"Hold expired at {hold.ExpiresAt:O}"
				};

				_ = await holdStore.UpdateHoldAsync(released, cancellationToken).ConfigureAwait(false);
				LogAutoReleased(hold.HoldId, hold.CaseReference);
			}
			catch (Exception ex)
			{
				LogReleaseFailed(hold.HoldId, ex);
			}
		}
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
}
