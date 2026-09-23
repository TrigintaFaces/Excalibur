// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

using System.Diagnostics.CodeAnalysis;

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
internal sealed partial class MaterializedViewRefreshService : BackgroundService
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
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Bucket D: the refresh work is trim-unsafe, but BackgroundService.ExecuteAsync is a BCL member that cannot carry the annotation and this type is internal, so no consumer signal is reachable from here. Tracked for a trim-safe view-serialization seam.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Bucket D: the refresh work is trim-unsafe, but BackgroundService.ExecuteAsync is a BCL member that cannot carry the annotation and this type is internal, so no consumer signal is reachable from here. Tracked for a trim-safe view-serialization seam.")]
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
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

	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private async Task RefreshWithRetryAsync(CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var attempts = 0;

		// Transient-fault retry is delegated to a Polly ResiliencePipeline (exponential backoff + jitter,
		// matching Excalibur.Dispatch.Resilience.Polly's own PollyRetryPolicyFactory pattern) instead of a
		// hand-rolled loop. MaxRetryCount == 0 means unlimited retries in the prior contract; Polly's
		// RetryStrategyOptions requires MaxRetryAttempts >= 1, so that maps to int.MaxValue.
		var pipeline = new ResiliencePipelineBuilder()
			.AddRetry(new RetryStrategyOptions
			{
				// A poison event is PERMANENT: the stored type will not resolve six seconds from now, so
				// retrying re-reads the same event, fails identically, and logs forever without progress.
				// Excluding MaterializedViewPoisonEventException here is what keeps the poison halt from
				// degrading back into the retry loop it exists to prevent -- ShouldHandle=false lets it
				// propagate out of ExecuteAsync unretried, on the FIRST occurrence.
				ShouldHandle = new PredicateBuilder().Handle<Exception>(
					ex => ex is not MaterializedViewPoisonEventException and not OperationCanceledException),
				MaxRetryAttempts = opts.MaxRetryCount > 0 ? opts.MaxRetryCount : int.MaxValue,
				Delay = opts.InitialRetryDelay,
				MaxDelay = opts.MaxRetryDelay,
				BackoffType = DelayBackoffType.Exponential,
				UseJitter = true,
				OnRetry = args =>
				{
					attempts = args.AttemptNumber + 1;
					LogRetrying(attempts, args.RetryDelay, args.Outcome.Exception!);
					return default;
				},
			})
			.Build();

		try
		{
			await pipeline.ExecuteAsync(
				static (svc, ct) => new ValueTask(svc.RefreshAllViewsAsync(ct)),
				this,
				cancellationToken).ConfigureAwait(false);
		}
		catch (MaterializedViewPoisonEventException ex)
		{
			LogPoisonEventHalt(ex.ViewName ?? "(all views)", ex.EventId ?? "(unknown)", ex.EventType ?? "(unknown)", ex.GlobalPosition, ex);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
		{
			// The pipeline exhausted every retry; the last failure surfaces here rather than being
			// silently swallowed, exactly as the prior loop's "give up after max retries" branch did.
			LogMaxRetriesExceeded(attempts, ex);
		}
	}

	[RequiresUnreferencedCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("The materialized view store serializes view types reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
				// The processor routes by the builder's DECLARED ViewName, not by the view type name.
				// Passing ViewType.Name here matches only when a builder happens to name itself after
				// its class, so every conventionally-named view silently caught up nothing at all.
				// Resolved inside the try: a malformed registration is a refresh failure like any
				// other, and must not escape and abandon the remaining views.
				var viewName = registration.GetViewName();

				LogRefreshingView(viewName);
				await processor.CatchUpAsync(viewName, cancellationToken).ConfigureAwait(false);
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

	[LoggerMessage(
		EventId = 3015,
		Level = LogLevel.Critical,
		Message = "Materialized view refresh HALTED for {ViewName}: event {EventId} at global position {GlobalPosition} has stored type '{EventType}', which cannot be resolved. The view is stopped and will not advance -- it is NOT silently skipping the event. Register the stored type name against the type it belongs to now if it moved namespace or assembly, or against a type no builder handles to pass over it deliberately.")]
	private partial void LogPoisonEventHalt(string viewName, string eventId, string eventType, long globalPosition, Exception ex);

	#endregion
}
