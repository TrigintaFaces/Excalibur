// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Hosting;

/// <summary>
/// Background service that periodically cleans up timed-out saga instances.
/// </summary>
/// <remarks>
/// <para>
/// This service polls the <see cref="Abstractions.ISagaStateStoreQuery"/> at a configurable interval
/// to find saga instances that have exceeded their timeout threshold. Timed-out sagas
/// are transitioned to the <see cref="SagaStatus.Expired"/> status and their state is
/// updated in the store.
/// </para>
/// <para>
/// Requires both <see cref="Abstractions.ISagaStateStore"/> and <see cref="Abstractions.ISagaStateStoreQuery"/>
/// to be registered in the service collection.
/// </para>
/// </remarks>
public sealed partial class SagaTimeoutCleanupService : BackgroundService
{
	private readonly Abstractions.ISagaStateStore _stateStore;
	private readonly Abstractions.ISagaStateStoreQuery _stateStoreQuery;
	private readonly ILogger<SagaTimeoutCleanupService> _logger;
	private readonly SagaTimeoutCleanupOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaTimeoutCleanupService"/> class.
	/// </summary>
	/// <param name="stateStore">The saga state store for updating saga status.</param>
	/// <param name="stateStoreQuery">The saga state store query for finding timed-out sagas.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">The cleanup service configuration options.</param>
	public SagaTimeoutCleanupService(
		Abstractions.ISagaStateStore stateStore,
		Abstractions.ISagaStateStoreQuery stateStoreQuery,
		ILogger<SagaTimeoutCleanupService> logger,
		IOptions<SagaTimeoutCleanupOptions> options)
	{
		_stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
		_stateStoreQuery = stateStoreQuery ?? throw new ArgumentNullException(nameof(stateStoreQuery));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var activity = SagaActivitySource.StartActivity("SagaTimeoutCleanupService.Execute");

		LogServiceStarting(_options.CleanupInterval.TotalSeconds);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await CleanupTimedOutSagasAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogCleanupCycleFailed(ex);
			}

			try
			{
				await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogServiceStopping();
	}

	private async Task CleanupTimedOutSagasAsync(CancellationToken cancellationToken)
	{
		using var activity = SagaActivitySource.StartActivity("CleanupTimedOutSagas");

		// Find running sagas that have exceeded the timeout threshold
		var runningSagas = await _stateStoreQuery
			.GetByStatusAsync(SagaStatus.Running, _options.BatchSize, cancellationToken)
			.ConfigureAwait(false);

		var cutoff = DateTime.UtcNow - _options.TimeoutThreshold;
		var cleanedUp = 0;

		foreach (var saga in runningSagas)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			if (saga.LastUpdatedAt < cutoff)
			{
				saga.Status = SagaStatus.Expired;
				saga.ErrorMessage = $"Saga timed out after {_options.TimeoutThreshold.TotalHours:F1} hours of inactivity.";
				saga.CompletedAt = DateTime.UtcNow;

				await _stateStore.UpdateStateAsync(saga, cancellationToken).ConfigureAwait(false);
				cleanedUp++;

				if (_options.EnableVerboseLogging)
				{
					LogSagaTimedOut(saga.SagaId, saga.SagaName);
				}
			}
		}

		if (cleanedUp > 0)
		{
			LogCleanupCompleted(cleanedUp);
		}

		_ = (activity?.SetTag("saga.cleanup.count", cleanedUp));
	}

	// ========================================
	// Source-generated logging (Event ID range: 121300-121399)
	// ========================================

	[LoggerMessage(121300, LogLevel.Information,
		"Saga timeout cleanup service starting with interval {IntervalSeconds}s")]
	private partial void LogServiceStarting(double intervalSeconds);

	[LoggerMessage(121301, LogLevel.Information,
		"Saga timeout cleanup service stopping")]
	private partial void LogServiceStopping();

	[LoggerMessage(121302, LogLevel.Information,
		"Saga timeout cleanup completed: {Count} sagas expired")]
	private partial void LogCleanupCompleted(int count);

	[LoggerMessage(121303, LogLevel.Debug,
		"Saga {SagaId} ({SagaName}) timed out and marked as expired")]
	private partial void LogSagaTimedOut(string sagaId, string sagaName);

	[LoggerMessage(121304, LogLevel.Error,
		"Saga timeout cleanup cycle failed, will retry next cycle")]
	private partial void LogCleanupCycleFailed(Exception ex);
}
