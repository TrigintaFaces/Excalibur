// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Patterns.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Background service that cleans up expired claim check payloads from the in-memory provider.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container as hosted service")]
internal partial class InMemoryClaimCheckCleanupService(
	InMemoryClaimCheckProvider provider,
	IOptions<ClaimCheckOptions> options,
	ILogger<InMemoryClaimCheckCleanupService> logger) : BackgroundService
{
	private readonly ClaimCheckOptions _options = options.Value;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.EnableCleanup)
		{
			LogCleanupDisabled();
			return;
		}

		LogCleanupServiceStarted(_options.CleanupInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Wait for cleanup interval
				await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);

				// Remove expired entries
				var removedCount = provider.RemoveExpiredEntries();

				if (removedCount > 0)
				{
					LogCleanupTaskCompleted(removedCount);
				}
				else
				{
					LogCleanupTaskNoExpiredEntries();
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
				break;
			}
			catch (Exception ex)
			{
				LogCleanupError(ex);

				// Continue running even if cleanup fails
			}
		}

		LogCleanupServiceStopped();
	}

	// Source-generated logging methods (Sprint 369 - EventId migration)
	[LoggerMessage(PatternsEventId.InMemoryCleanupDisabled, LogLevel.Information,
		"In-memory claim check cleanup is disabled")]
	private partial void LogCleanupDisabled();

	[LoggerMessage(PatternsEventId.InMemoryCleanupStartedInterval, LogLevel.Information,
		"In-memory claim check cleanup service started with interval {Interval}")]
	private partial void LogCleanupServiceStarted(TimeSpan interval);

	[LoggerMessage(PatternsEventId.InMemoryExpiredClaimsRemoved, LogLevel.Information,
		"In-memory claim check cleanup completed: {RemovedCount} expired entries removed")]
	private partial void LogCleanupTaskCompleted(int removedCount);

	[LoggerMessage(PatternsEventId.InMemoryCleanupTaskRunning, LogLevel.Debug,
		"In-memory claim check cleanup completed: no expired entries found")]
	private partial void LogCleanupTaskNoExpiredEntries();

	[LoggerMessage(PatternsEventId.InMemoryCleanupError, LogLevel.Error, "Error during in-memory claim check cleanup")]
	private partial void LogCleanupError(Exception ex);

	[LoggerMessage(PatternsEventId.InMemoryCleanupServiceStopped, LogLevel.Information,
		"In-memory claim check cleanup service stopped")]
	private partial void LogCleanupServiceStopped();
}
