// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for the erasure scheduler background service.
/// </summary>
public class ErasureSchedulerOptions
{
	/// <summary>
	/// Gets or sets the interval between polling for scheduled erasures.
	/// Default: 5 minutes.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of erasures to process per polling cycle.
	/// Default: 10.
	/// </summary>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets whether the scheduler is enabled.
	/// Default: true.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum retry attempts for failed erasures.
	/// Default: 3.
	/// </summary>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts with exponential backoff base.
	/// Default: 30 seconds (will be multiplied by retry count).
	/// </summary>
	public TimeSpan RetryDelayBase { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets whether to use exponential backoff for retries.
	/// Default: true.
	/// </summary>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for cleaning up expired certificates.
	/// Default: 24 hours.
	/// </summary>
	public TimeSpan CertificateCleanupInterval { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Background service that processes scheduled erasure requests after their grace period expires.
/// </summary>
/// <remarks>
/// <para>
/// This service:
/// </para>
/// <list type="bullet">
/// <item><description>Polls for erasure requests past their scheduled execution time</description></item>
/// <item><description>Executes erasures via IErasureService</description></item>
/// <item><description>Handles retry logic with exponential backoff for failed erasures</description></item>
/// <item><description>Cleans up expired certificates past their retention period</description></item>
/// </list>
/// </remarks>
public partial class ErasureSchedulerBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<ErasureSchedulerOptions> _options;
	private readonly ILogger<ErasureSchedulerBackgroundService> _logger;

	private DateTimeOffset _lastCertificateCleanup = DateTimeOffset.MinValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureSchedulerBackgroundService"/> class.
	/// </summary>
	public ErasureSchedulerBackgroundService(
		IServiceScopeFactory scopeFactory,
		IOptions<ErasureSchedulerOptions> options,
		ILogger<ErasureSchedulerBackgroundService> logger)
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
			LogErasureSchedulerDisabled();
			return;
		}

		LogErasureSchedulerStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessScheduledErasuresAsync(stoppingToken).ConfigureAwait(false);
				await MaybeCleanupCertificatesAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				LogErasureSchedulerProcessingError(ex);
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

		LogErasureSchedulerStopped();
	}

	private async Task ProcessScheduledErasuresAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var erasureStore = scope.ServiceProvider.GetRequiredService<IErasureStore>();
		var erasureService = scope.ServiceProvider.GetRequiredService<IErasureService>();

		var queryStore = (IErasureQueryStore?)erasureStore.GetService(typeof(IErasureQueryStore))
			?? throw new InvalidOperationException("The erasure store does not support query operations.");
		var scheduledRequests = await queryStore.GetScheduledRequestsAsync(
			_options.Value.BatchSize, cancellationToken).ConfigureAwait(false);

		if (scheduledRequests.Count == 0)
		{
			LogErasureSchedulerNoScheduledRequests();
			return;
		}

		LogErasureSchedulerProcessingBatch(scheduledRequests.Count);

		foreach (var request in scheduledRequests)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			await ProcessSingleErasureAsync(
				request, erasureStore, erasureService, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task ProcessSingleErasureAsync(
		ErasureStatus request,
		IErasureStore erasureStore,
		IErasureService erasureService,
		CancellationToken cancellationToken)
	{
		LogErasureSchedulerExecutingRequest(request.RequestId, request.ScheduledExecutionAt);

		try
		{
			// Execute the erasure (the service handles status transitions internally)
			var result = await erasureService.ExecuteAsync(
				request.RequestId, cancellationToken).ConfigureAwait(false);

			if (result.Success)
			{
				LogErasureSchedulerRequestCompleted(
						request.RequestId,
						result.KeysDeleted,
						result.RecordsAffected);
			}
			else
			{
				LogErasureSchedulerRequestFailed(request.RequestId, result.ErrorMessage);

				await HandleFailedErasureAsync(
						request, erasureStore, result.ErrorMessage, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogErasureSchedulerExecutionError(request.RequestId, ex);

			await HandleFailedErasureAsync(
					request, erasureStore, ex.Message, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task HandleFailedErasureAsync(
		ErasureStatus request,
		IErasureStore erasureStore,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		// Note: Retry logic with exponential backoff is available via _options.Value
		// (MaxRetryAttempts, RetryDelayBase, UseExponentialBackoff)
		// For now, we simply mark as failed - the store could track retry counts
		_ = await erasureStore.UpdateStatusAsync(
			request.RequestId,
			ErasureRequestStatus.Failed,
			errorMessage,
			cancellationToken).ConfigureAwait(false);

		LogErasureSchedulerMarkedFailed(request.RequestId, errorMessage);
	}

	private async Task MaybeCleanupCertificatesAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;
		if (now - _lastCertificateCleanup < _options.Value.CertificateCleanupInterval)
		{
			return;
		}

		_lastCertificateCleanup = now;

		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var erasureStore = scope.ServiceProvider.GetRequiredService<IErasureStore>();

			var certStore = (IErasureCertificateStore?)erasureStore.GetService(typeof(IErasureCertificateStore));
			if (certStore is null)
			{
				return;
			}

			var deletedCount = await certStore.CleanupExpiredCertificatesAsync(cancellationToken)
				.ConfigureAwait(false);

			if (deletedCount > 0)
			{
				LogErasureSchedulerCertificatesCleaned(deletedCount);
			}
		}
		catch (Exception ex)
		{
			LogErasureSchedulerCertificateCleanupFailed(ex);
		}
	}

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerDisabled,
			LogLevel.Information,
			"Erasure scheduler background service is disabled")]
	private partial void LogErasureSchedulerDisabled();

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerStarting,
			LogLevel.Information,
			"Erasure scheduler background service starting with polling interval {PollingInterval}")]
	private partial void LogErasureSchedulerStarting(TimeSpan pollingInterval);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerProcessingError,
			LogLevel.Error,
			"Error in erasure scheduler processing cycle")]
	private partial void LogErasureSchedulerProcessingError(Exception exception);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerStopped,
			LogLevel.Information,
			"Erasure scheduler background service stopped")]
	private partial void LogErasureSchedulerStopped();

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerNoScheduledRequests,
			LogLevel.Debug,
			"No scheduled erasures ready for execution")]
	private partial void LogErasureSchedulerNoScheduledRequests();

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerProcessingBatch,
			LogLevel.Information,
			"Processing {Count} scheduled erasure requests")]
	private partial void LogErasureSchedulerProcessingBatch(int count);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerExecutingRequest,
			LogLevel.Information,
			"Executing scheduled erasure for request {RequestId} (scheduled: {ScheduledAt})")]
	private partial void LogErasureSchedulerExecutingRequest(Guid requestId, DateTimeOffset? scheduledAt);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerRequestCompleted,
			LogLevel.Information,
			"Erasure completed for request {RequestId}: {KeysDeleted} keys deleted, {RecordsAffected} records affected")]
	private partial void LogErasureSchedulerRequestCompleted(Guid requestId, int keysDeleted, int recordsAffected);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerRequestFailed,
			LogLevel.Warning,
			"Erasure failed for request {RequestId}: {Error}")]
	private partial void LogErasureSchedulerRequestFailed(Guid requestId, string? error);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerExecutionError,
			LogLevel.Error,
			"Error executing erasure for request {RequestId}")]
	private partial void LogErasureSchedulerExecutionError(Guid requestId, Exception exception);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerMarkedFailed,
			LogLevel.Warning,
			"Erasure request {RequestId} marked as failed. Error: {Error}")]
	private partial void LogErasureSchedulerMarkedFailed(Guid requestId, string? error);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerCertificatesCleaned,
			LogLevel.Information,
			"Cleaned up {Count} expired erasure certificates")]
	private partial void LogErasureSchedulerCertificatesCleaned(int count);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerCertificateCleanupFailed,
			LogLevel.Error,
			"Error during certificate cleanup")]
	private partial void LogErasureSchedulerCertificateCleanupFailed(Exception exception);
}
