// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Services;

/// <summary>
/// Background service that periodically refreshes materialized views from the event stream.
/// </summary>
/// <remarks>
/// <para>
/// This service supports two scheduling modes:
/// <list type="bullet">
/// <item><b>Interval-based:</b> Refresh at fixed intervals (default: 30 seconds)</item>
/// <item><b>Cron-based:</b> Refresh according to a cron schedule (takes precedence if configured)</item>
/// </list>
/// </para>
/// <para>
/// The service implements exponential backoff retry for transient failures and supports
/// graceful shutdown via <see cref="CancellationToken"/>.
/// </para>
/// </remarks>
public sealed partial class MaterializedViewRefreshService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<MaterializedViewRefreshOptions> _options;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<MaterializedViewRefreshService> _logger;
	private readonly CronSchedule? _cronSchedule;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewRefreshService"/> class.
	/// </summary>
	/// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
	/// <param name="options">The refresh options.</param>
	/// <param name="timeProvider">The time provider for testability.</param>
	/// <param name="logger">The logger.</param>
	public MaterializedViewRefreshService(
		IServiceScopeFactory scopeFactory,
		IOptions<MaterializedViewRefreshOptions> options,
		TimeProvider timeProvider,
		ILogger<MaterializedViewRefreshService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Parse cron expression if configured
		if (!string.IsNullOrWhiteSpace(_options.Value.CronExpression))
		{
			if (CronSchedule.TryParse(_options.Value.CronExpression, out var schedule))
			{
				_cronSchedule = schedule;
				LogCronScheduleConfigured(_options.Value.CronExpression);
			}
			else
			{
				LogInvalidCronExpression(_options.Value.CronExpression);
			}
		}
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var opts = _options.Value;

		if (!opts.Enabled)
		{
			LogServiceDisabled();
			return;
		}

		LogServiceStarting();

		// Catch-up on startup if configured
		if (opts.CatchUpOnStartup)
		{
			LogCatchUpStarting();
			await RefreshAllViewsAsync(stoppingToken).ConfigureAwait(false);
			LogCatchUpCompleted();
		}

		// Main refresh loop
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var delay = GetNextDelay();
				if (delay > TimeSpan.Zero)
				{
					LogWaitingForNextRefresh(delay);
					await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
				}

				if (stoppingToken.IsCancellationRequested)
				{
					break;
				}

				await RefreshWithRetryAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Graceful shutdown
				break;
			}
			catch (Exception ex)
			{
				LogUnexpectedError(ex);
				// Continue the loop after unexpected errors
			}
		}

		LogServiceStopping();
	}

	private TimeSpan GetNextDelay()
	{
		var opts = _options.Value;

		// Cron-based scheduling takes precedence
		if (_cronSchedule is not null)
		{
			return _cronSchedule.GetDelayUntilNext(_timeProvider.GetUtcNow());
		}

		// Fall back to interval-based scheduling
		return opts.RefreshInterval ?? TimeSpan.FromSeconds(30);
	}

	private async Task RefreshWithRetryAsync(CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var retryCount = 0;
		var currentDelay = opts.InitialRetryDelay;

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await RefreshAllViewsAsync(cancellationToken).ConfigureAwait(false);
				return; // Success
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw; // Re-throw cancellation
			}
			catch (Exception ex)
			{
				retryCount++;

				if (opts.MaxRetryCount > 0 && retryCount >= opts.MaxRetryCount)
				{
					LogMaxRetriesExceeded(retryCount, ex);
					return; // Give up after max retries
				}

				LogRetrying(retryCount, currentDelay, ex);
				await Task.Delay(currentDelay, _timeProvider, cancellationToken).ConfigureAwait(false);

				// Exponential backoff with jitter (non-security context - jitter for retry spacing)
#pragma warning disable CA5394 // Do not use insecure randomness
				var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
#pragma warning restore CA5394
				currentDelay = TimeSpan.FromTicks(Math.Min(
					currentDelay.Ticks * 2,
					opts.MaxRetryDelay.Ticks)) + jitter;
			}
		}
	}

	private async Task RefreshAllViewsAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();

		var processor = scope.ServiceProvider.GetService<IMaterializedViewProcessor>();
		if (processor is null)
		{
			LogNoProcessorRegistered();
			return;
		}

		var registrations = scope.ServiceProvider.GetServices<MaterializedViewBuilderRegistration>();
		var viewCount = 0;

		foreach (var registration in registrations)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			try
			{
				LogRefreshingView(registration.ViewType.Name);
				await processor.CatchUpAsync(registration.ViewType.Name, cancellationToken).ConfigureAwait(false);
				viewCount++;
			}
			catch (Exception ex)
			{
				LogViewRefreshFailed(registration.ViewType.Name, ex);
				throw; // Let retry logic handle it
			}
		}

		LogRefreshCompleted(viewCount);
	}

	#region Logging

	[LoggerMessage(
		EventId = 3000,
		Level = LogLevel.Information,
		Message = "Materialized view refresh service starting")]
	private partial void LogServiceStarting();

	[LoggerMessage(
		EventId = 3001,
		Level = LogLevel.Information,
		Message = "Materialized view refresh service stopping")]
	private partial void LogServiceStopping();

	[LoggerMessage(
		EventId = 3002,
		Level = LogLevel.Information,
		Message = "Materialized view refresh service is disabled")]
	private partial void LogServiceDisabled();

	[LoggerMessage(
		EventId = 3003,
		Level = LogLevel.Information,
		Message = "Cron schedule configured: {CronExpression}")]
	private partial void LogCronScheduleConfigured(string cronExpression);

	[LoggerMessage(
		EventId = 3004,
		Level = LogLevel.Warning,
		Message = "Invalid cron expression: {CronExpression}. Falling back to interval-based scheduling.")]
	private partial void LogInvalidCronExpression(string cronExpression);

	[LoggerMessage(
		EventId = 3005,
		Level = LogLevel.Debug,
		Message = "Waiting {Delay} until next refresh")]
	private partial void LogWaitingForNextRefresh(TimeSpan delay);

	[LoggerMessage(
		EventId = 3006,
		Level = LogLevel.Information,
		Message = "Starting catch-up refresh on startup")]
	private partial void LogCatchUpStarting();

	[LoggerMessage(
		EventId = 3007,
		Level = LogLevel.Information,
		Message = "Catch-up refresh completed")]
	private partial void LogCatchUpCompleted();

	[LoggerMessage(
		EventId = 3008,
		Level = LogLevel.Debug,
		Message = "Refreshing view: {ViewName}")]
	private partial void LogRefreshingView(string viewName);

	[LoggerMessage(
		EventId = 3009,
		Level = LogLevel.Information,
		Message = "Refresh cycle completed. Processed {ViewCount} views.")]
	private partial void LogRefreshCompleted(int viewCount);

	[LoggerMessage(
		EventId = 3010,
		Level = LogLevel.Error,
		Message = "Failed to refresh view: {ViewName}")]
	private partial void LogViewRefreshFailed(string viewName, Exception ex);

	[LoggerMessage(
		EventId = 3011,
		Level = LogLevel.Warning,
		Message = "Retry attempt {RetryCount} after {Delay}")]
	private partial void LogRetrying(int retryCount, TimeSpan delay, Exception ex);

	[LoggerMessage(
		EventId = 3012,
		Level = LogLevel.Error,
		Message = "Maximum retry attempts ({RetryCount}) exceeded")]
	private partial void LogMaxRetriesExceeded(int retryCount, Exception ex);

	[LoggerMessage(
		EventId = 3013,
		Level = LogLevel.Warning,
		Message = "No IMaterializedViewProcessor registered. Skipping refresh.")]
	private partial void LogNoProcessorRegistered();

	[LoggerMessage(
		EventId = 3014,
		Level = LogLevel.Error,
		Message = "Unexpected error in refresh loop")]
	private partial void LogUnexpectedError(Exception ex);

	#endregion
}
