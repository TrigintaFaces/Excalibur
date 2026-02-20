// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Patterns.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Background service that cleans up expired claim check payloads.
/// </summary>
/// <remarks>
/// <para>
/// This service periodically invokes <see cref="IClaimCheckCleanupProvider.CleanupExpiredAsync"/>
/// to remove expired claim check entries from the underlying storage. The registered
/// <see cref="IClaimCheckProvider"/> must also implement <see cref="IClaimCheckCleanupProvider"/>
/// for cleanup to function; otherwise the service logs a warning and exits gracefully.
/// </para>
/// <para>
/// Cleanup behavior is configured via <see cref="ClaimCheckOptions"/>:
/// <list type="bullet">
///   <item><see cref="ClaimCheckOptions.EnableCleanup"/> — master toggle (default: <c>true</c>)</item>
///   <item><see cref="ClaimCheckOptions.CleanupInterval"/> — interval between cleanup cycles (default: 1 hour)</item>
///   <item><see cref="ClaimCheckCleanupOptions.CleanupBatchSize"/> — max entries per cycle (default: 1000)</item>
/// </list>
/// </para>
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated by DI container as hosted service")]
internal partial class ClaimCheckCleanupService(
	IClaimCheckProvider provider,
	IOptions<ClaimCheckOptions> options,
	ILogger<ClaimCheckCleanupService> logger) : BackgroundService
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

		// Check if the provider supports cleanup via the ISP interface.
		if (provider is not IClaimCheckCleanupProvider cleanupProvider)
		{
			LogCleanupProviderNotAvailable(provider.GetType().Name);
			return;
		}

		LogCleanupServiceStarted(_options.CleanupInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);

				if (stoppingToken.IsCancellationRequested)
				{
					break;
				}

				await PerformCleanupAsync(cleanupProvider, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
				break;
			}
			catch (Exception ex)
			{
				LogCleanupError(ex);

				// Back off before retrying to avoid tight error loops
				try
				{
					await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		LogCleanupServiceStopped();
	}

	/// <summary>
	/// Performs a single cleanup cycle, removing expired entries in batches.
	/// </summary>
	private async Task PerformCleanupAsync(
		IClaimCheckCleanupProvider cleanupProvider,
		CancellationToken cancellationToken)
	{
		var removedCount = await cleanupProvider
			.CleanupExpiredAsync(_options.Cleanup.CleanupBatchSize, cancellationToken)
			.ConfigureAwait(false);

		if (removedCount > 0)
		{
			LogCleanupExpiredRemoved(removedCount);
		}
		else
		{
			LogCleanupNoExpiredEntries();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(PatternsEventId.ClaimCheckCleanupDisabled, LogLevel.Information, "Claim check cleanup is disabled")]
	private partial void LogCleanupDisabled();

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupStartedInterval, LogLevel.Information,
		"Claim check cleanup service started with interval {Interval}")]
	private partial void LogCleanupServiceStarted(TimeSpan interval);

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupExpiredRemoved, LogLevel.Information,
		"Claim check cleanup completed: {RemovedCount} expired entries removed")]
	private partial void LogCleanupExpiredRemoved(int removedCount);

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupNoExpiredEntries, LogLevel.Debug,
		"Claim check cleanup completed: no expired entries found")]
	private partial void LogCleanupNoExpiredEntries();

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupError, LogLevel.Error, "Error during claim check cleanup")]
	private partial void LogCleanupError(Exception ex);

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupServiceStopped, LogLevel.Information, "Claim check cleanup service stopped")]
	private partial void LogCleanupServiceStopped();

	[LoggerMessage(PatternsEventId.ClaimCheckCleanupProviderNotAvailable, LogLevel.Warning,
		"Claim check cleanup service cannot run: provider '{ProviderType}' does not implement IClaimCheckCleanupProvider. " +
		"Cleanup must be handled by the storage provider's native TTL/lifecycle policies, or use a provider that implements IClaimCheckCleanupProvider")]
	private partial void LogCleanupProviderNotAvailable(string providerType);
}
