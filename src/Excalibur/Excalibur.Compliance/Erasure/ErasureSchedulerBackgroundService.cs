// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Configuration options for the erasure scheduler background service.
/// </summary>
public sealed class ErasureSchedulerOptions
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
internal sealed partial class ErasureSchedulerBackgroundService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<ErasureSchedulerOptions> _options;
	private readonly ILogger<ErasureSchedulerBackgroundService> _logger;

	private readonly ConcurrentDictionary<Guid, int> _retryAttempts = new();
	private DateTimeOffset _lastCertificateCleanup = DateTimeOffset.MinValue;

	/// <summary>
	/// Initializes a new instance of the <c>ErasureSchedulerBackgroundService</c> class.
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
				await EnsureRetentionDeclarationsRecordedAsync(stoppingToken).ConfigureAwait(false);
				await ReevaluateHoldDeferredErasuresAsync(stoppingToken).ConfigureAwait(false);
				await ProcessScheduledErasuresAsync(stoppingToken).ConfigureAwait(false);
				await CompletePendingErasuresAsync(stoppingToken).ConfigureAwait(false);
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
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogErasureSchedulerStopped();
	}

	/// <summary>
	/// Records this instance's retention declarations in the deployment-wide record, once per process.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>At the TOP of the poll, before any batch is fetched</b>, and NOT before this instance's first
	/// erasure. The advance protects OTHER instances from this one, so until it has happened an instance
	/// that holds fewer declarations cannot detect that it is behind. Anchored to the first erasure, that
	/// protection would begin days after the deploy in a low-volume deployment, and the first post-deploy
	/// erasure would be decided by which instance's poll won.
	/// </para>
	/// <para>
	/// <b>Isolated like the other passes, and deliberately NOT a readiness check.</b> A compliance-store
	/// outage must not stop the application serving; it withholds only this instance's erasures, which is
	/// the intended direction — the staleness gate on the erasure path refuses while the record cannot be
	/// read. The latch stays open on failure, so the next poll retries.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">A token that cancels the recording.</param>
	/// <returns>A task that completes when the attempt has been made.</returns>
	private async Task EnsureRetentionDeclarationsRecordedAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var latch = scope.ServiceProvider.GetService<ErasureRetentionAdvanceLatch>();
			if (latch is not null)
			{
				await latch.EnsureAdvancedAsync(
					scope.ServiceProvider.GetService<IErasureRetentionDeclarationLedger>(),
					cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
#pragma warning disable CA1031 // One pass's failure must not stop the others.
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogErasureRetentionAdvanceFailed(ex);
		}
	}

	/// <summary>
	/// Returns requests a legal hold deferred to the executable state once no hold blocks them. Isolated
	/// from the other passes so a failure here never stops scheduled erasures from executing, and vice
	/// versa. It runs BEFORE the execution pass so a request it resumes is picked up in the same poll
	/// rather than waiting for the next one.
	/// </summary>
	private async Task ReevaluateHoldDeferredErasuresAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var processor = scope.ServiceProvider.GetService<IErasureHoldReevaluationProcessor>();
			if (processor is not null)
			{
				_ = await processor.ReevaluateDeferredErasuresAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
#pragma warning disable CA1031 // One pass's failure must not stop the others.
		catch (Exception ex)
#pragma warning restore CA1031
		{
			LogErasureSchedulerHoldReevaluationPassError(ex);
		}
	}

	private async Task ProcessScheduledErasuresAsync(CancellationToken cancellationToken)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var erasureStore = scope.ServiceProvider.GetRequiredService<IErasureStore>();
		var erasureExecutor = scope.ServiceProvider.GetRequiredService<IErasureExecutor>();

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
				request, erasureStore, erasureExecutor, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Revisits requests awaiting a provider's key destruction. Isolated from the execution pass so that a
	/// failure here (for example a verification service that cannot be resolved) never stops scheduled erasures
	/// from executing, and vice versa.
	/// </summary>
	private async Task CompletePendingErasuresAsync(CancellationToken cancellationToken)
	{
		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var processor = scope.ServiceProvider.GetService<IErasureCompletionProcessor>();
			if (processor is not null)
			{
				_ = await processor.CompletePendingErasuresAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogErasureSchedulerCompletionPassError(ex);
		}
	}

	private async Task ProcessSingleErasureAsync(
		ErasureStatus request,
		IErasureStore erasureStore,
		IErasureExecutor erasureExecutor,
		CancellationToken cancellationToken)
	{
		IDisposable tenantScope;
		try
		{
			tenantScope = ErasureRequestTenantScope.Begin(request);
		}
		catch (Exception ex)
		{
			LogErasureSchedulerExecutionError(request.RequestId, ex);
			return;
		}

		using var ownedScope = tenantScope;
		LogErasureSchedulerExecutingRequest(request.RequestId, request.ScheduledExecutionAt);
		try
		{
			ErasureExecutionResult result;
			// Execute the erasure (the service handles status transitions internally)
			try
			{
				result = await erasureExecutor.ExecuteAsync(request.RequestId, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogErasureSchedulerExecutionError(request.RequestId, ex);
				await HandleFailedErasureAsync(request, erasureStore, ex.Message, cancellationToken).ConfigureAwait(false);
				return;
			}

			if (result.Success)
			{
				LogErasureSchedulerRequestCompleted(
						request.RequestId,
						result.KeysDeleted,
						result.RecordsAffected);
			}
			else if (result.Outcome == ErasureExecutionOutcome.Refused)
			{
				// A REFUSAL IS NOT A FAULT, so it must not spend this request's fault budget. The executor
				// declined and destroyed nothing -- a legal hold deferring the erasure, another executor
				// already owning it, an ineligible status. Routing that into the failure path increments a
				// per-process retry counter and, at exhaustion, writes a terminal Failed that the
				// scheduler's own query never returns and no pass enumerates: a correct withholding
				// converted into a permanent non-erasure, within MaxRetryAttempts poll intervals.
				//
				// The request is left exactly as the executor left it, so whatever re-enumerates that
				// state picks it up -- a still-Scheduled request on the next poll, a deferred one through
				// the hold re-evaluation pass.
				_ = _retryAttempts.TryRemove(request.RequestId, out _);
				LogErasureSchedulerRequestRefused(request.RequestId, result.ErrorMessage);
			}
			else
			{
				LogErasureSchedulerRequestFailed(request.RequestId, result.ErrorMessage);

				await HandleFailedErasureAsync(
						request, erasureStore, result.ErrorMessage, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogErasureSchedulerExecutionError(request.RequestId, ex);
			// A tenant-scoped retry failed. The owner scope remains active through this diagnostic.
		}
	}

	private async Task HandleFailedErasureAsync(
		ErasureStatus request,
		IErasureStore erasureStore,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;

		// A request that executed and is now waiting on the provider's key destruction is not a failure and must
		// NOT be put back to Scheduled: that would execute it again, re-attempting to destroy keys that are
		// already irreversibly scheduled. It is completed by the completion pass instead.
		//
		// Nor is a request another instance has claimed (InProgress), completed, or that was cancelled. The
		// execution result cannot say "someone else owns this" -- it reports "invalid status" or "concurrent
		// execution" as a failure -- and resetting such a request to Scheduled hands it back for a second
		// execution, a second certificate, or a cancellation beside keys that are already being destroyed.
		var current = await erasureStore.GetStatusAsync(request.RequestId, cancellationToken).ConfigureAwait(false);

		// A LAWFUL DEFERRAL IS NOT A FAULT, and treating it as one was wrong in three separate ways.
		//
		// A legal hold is the system working correctly, not an error. Routed through the retry path, a
		// deferral spent the request's fault budget on a state no retry can change -- the hold is still
		// there on the next cycle -- and at exhaustion the status was overwritten with a terminal "failed
		// after N attempts". That record is INDISTINGUISHABLE from a genuine execution failure: a key
		// provider outage, a broken store. The status that explained WHY is gone, and the hold's lawful
		// basis, case reference and description survive only as a sentence inside an error message. A
		// consumer handing an auditor a list of failed erasures could not tell which were deferred by law
		// and which were broken.
		//
		// Leaving the deferral standing is also what makes it discoverable: BlockedByLegalHold is a
		// durable, self-describing state a re-evaluation pass can find, where Failed is terminal and
		// enumerated by nothing.
		if (current?.Status is ErasureRequestStatus.BlockedByLegalHold)
		{
			_ = _retryAttempts.TryRemove(request.RequestId, out _);
			LogErasureDeferredByLegalHold(request.RequestId);
			return;
		}

		if (current?.Status is ErasureRequestStatus.AwaitingKeyDestruction
			or ErasureRequestStatus.InProgress
			or ErasureRequestStatus.Completed
			or ErasureRequestStatus.Cancelled)
		{
			_ = _retryAttempts.TryRemove(request.RequestId, out _);
			LogErasureSchedulerNotRescheduledAwaitingDestruction(request.RequestId);
			return;
		}

		var attemptCount = _retryAttempts.AddOrUpdate(request.RequestId, 1, static (_, current) => current + 1);

		if (attemptCount < opts.MaxRetryAttempts)
		{
			// Reschedule with backoff delay
			var delay = opts.UseExponentialBackoff
				? TimeSpan.FromTicks(opts.RetryDelayBase.Ticks * (1L << attemptCount))
				: opts.RetryDelayBase;

			// Set status back to Scheduled so the next polling cycle picks it up after the delay.
			//
			// THE STORE MAY REFUSE THIS, and the refusal is the authority rather than the check above.
			// That check reads the current status and then writes, so a claim landing between the two is
			// invisible to it; the store's write of Scheduled is conditional on the state it finds, which
			// is the only place the test and the write are one operation. A false return therefore means
			// another instance now owns the request -- exactly the case the check above is trying to
			// detect, caught where it can actually be caught.
			// NO CLAIM, and null is the STRICTER argument rather than an omission. This is a SUPERVISOR: it
			// cannot obtain an identity, because the identity is minted inside the executor and never comes
			// back out. Passing null says "do this only if nobody is working on the request", which is exactly
			// what a supervisor means -- so the write is refused while a live claim exists and applies once
			// that claim lapses.
			var rescheduled = await erasureStore.UpdateStatusAsync(
				request.RequestId,
				ErasureRequestStatus.Scheduled,
				expectedStatus: null,
				$"Retry {attemptCount}/{opts.MaxRetryAttempts}: {errorMessage}",
				claimIdentity: null,
				cancellationToken).ConfigureAwait(false);

			// Tested BY NAME. Anything other than Applied means this instance is not retrying: either a live
			// claim holds the request, or its status moved on. Both oblige the same thing here -- release the
			// attempt budget and stand down -- so they are not separated.
			if (rescheduled != ErasureWriteOutcome.Applied)
			{
				// Logged, and the attempt counter is released: this instance is not retrying anything, so
				// holding a count against a request another instance owns would spend that request's
				// budget on an attempt that never happened and could mark it permanently Failed early.
				_ = _retryAttempts.TryRemove(request.RequestId, out _);
				LogErasureSchedulerNotRescheduledAwaitingDestruction(request.RequestId);
				return;
			}

			// Logged only once the reschedule actually landed. Announcing a retry before the write meant
			// the log asserted a retry that the store may have refused.
			LogErasureSchedulerRetrying(request.RequestId, attemptCount, opts.MaxRetryAttempts, delay);
		}
		else
		{
			// Retries exhausted -- mark as permanently failed and clean up tracking
			_ = _retryAttempts.TryRemove(request.RequestId, out _);

			// TESTED BY NAME, like the reschedule above, and for the same reason: null is the stricter
			// argument, so anything other than Applied means a live claim holds the request or its status
			// moved on. Marking a request permanently Failed is the one write that cannot be undone by a
			// later pass, so a refusal here must not be silent -- discarding it meant an operator saw
			// nothing while the request stayed in whatever state the other instance left it.
			var failed = await erasureStore.UpdateStatusAsync(
				request.RequestId,
				ErasureRequestStatus.Failed,
				expectedStatus: null,
				$"Failed after {attemptCount} attempts: {errorMessage}",
				claimIdentity: null,
				cancellationToken).ConfigureAwait(false);

			if (failed != ErasureWriteOutcome.Applied)
			{
				LogErasureSchedulerNotRescheduledAwaitingDestruction(request.RequestId);
				return;
			}

			LogErasureSchedulerMarkedFailed(request.RequestId, errorMessage);
		}
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
			ComplianceEventId.ErasureDeferredByLegalHold,
			LogLevel.Information,
			"Erasure request {RequestId} is deferred by an active legal hold. This is lawful and is NOT a failure: the deferral is left standing rather than retried or marked failed, and the request becomes executable again once no hold blocks it")]
	private partial void LogErasureDeferredByLegalHold(Guid requestId);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerRetrying,
			LogLevel.Information,
			"Erasure request {RequestId} scheduled for retry (attempt {AttemptCount}/{MaxAttempts}, delay: {Delay})")]
	private partial void LogErasureSchedulerRetrying(Guid requestId, int attemptCount, int maxAttempts, TimeSpan delay);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerCompletionPassError,
			LogLevel.Error,
			"Error in erasure scheduler completion pass for requests awaiting key destruction")]
	private partial void LogErasureSchedulerCompletionPassError(Exception exception);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerHoldReevaluationPassError,
			LogLevel.Error,
			"Error in erasure scheduler hold re-evaluation pass for requests a legal hold deferred")]
	private partial void LogErasureSchedulerHoldReevaluationPassError(Exception exception);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerRequestRefused,
			LogLevel.Information,
			"Erasure request {RequestId} was REFUSED rather than attempted, so it does not consume the fault budget: {Reason}")]
	private partial void LogErasureSchedulerRequestRefused(Guid requestId, string? reason);

	[LoggerMessage(
			ComplianceEventId.ErasureRetentionAdvanceFailed,
			LogLevel.Error,
			"This instance could not record its retention declarations, so other instances cannot yet detect that they hold fewer; erasures are withheld until it succeeds and it is retried on the next poll")]
	private partial void LogErasureRetentionAdvanceFailed(Exception exception);

	[LoggerMessage(
			ComplianceEventId.ErasureSchedulerNotRescheduledAwaitingDestruction,
			LogLevel.Information,
			"Erasure request {RequestId} is awaiting key destruction, owned by another execution, completed or cancelled; it is not rescheduled for execution")]
	private partial void LogErasureSchedulerNotRescheduledAwaitingDestruction(Guid requestId);

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

internal static class ErasureRequestTenantScope
{
	public static IDisposable Begin(ErasureStatus request)
	{
		var tenantId = request.TenantId;
		if (tenantId is not null && string.IsNullOrWhiteSpace(tenantId))
		{
			throw new ArgumentException("A recorded erasure tenant identity cannot be blank.", nameof(request));
		}
		return TenantContextHolder.BeginScope(tenantId ?? TenantScope.UntenantedSentinel);
	}
}
