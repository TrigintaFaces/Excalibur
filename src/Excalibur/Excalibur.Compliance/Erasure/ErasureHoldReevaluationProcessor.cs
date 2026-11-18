// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Default <see cref="IErasureHoldReevaluationProcessor"/>: finds every request in
/// <see cref="ErasureRequestStatus.BlockedByLegalHold"/>, re-reads the hold store under that request's own
/// recorded tenant, and returns the ones no hold blocks to <see cref="ErasureRequestStatus.Scheduled"/>.
/// </summary>
/// <remarks>
/// <para>
/// The request set is read in full before any request is resumed. Resuming a request removes it from the
/// status filter, so resuming while paging would shift later pages and skip requests -- they would not be
/// lost, but a steady deferred population would take an unbounded number of polls to drain and the
/// degradation would be in liveness and silent. Concurrent mutations can still affect discovery; later
/// polls revisit survivors. Discovery is neither a snapshot nor a claim.
/// </para>
/// <para>
/// THE PASS MUST NOT ACQUIRE A SHORTCUT. Safety lives in the executor, not here: it writes only the
/// scheduled status, never the claimed one, it does not bypass the execution entry point, and it does not
/// widen any accepted-status gate. The write is the store's own conditional one, so a request another
/// executor claimed in the meantime is refused rather than re-offered.
/// </para>
/// <para>
/// Returning a deferred request to the executable state re-exposes nothing, and that rests on an ordering
/// property rather than on luck: both writes of the deferred status happen after the execution claim and
/// before any irreversible act, so the request did no destructive work and re-entry is idempotent.
/// </para>
/// </remarks>
internal sealed partial class ErasureHoldReevaluationProcessor : IErasureHoldReevaluationProcessor
{
	private const int PageSize = 500;

	private readonly IErasureStore _store;
	private readonly ILegalHoldService _legalHoldService;
	private readonly ILogger<ErasureHoldReevaluationProcessor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureHoldReevaluationProcessor"/> class.
	/// </summary>
	/// <param name="store">The erasure store to read deferred requests from and write the resume to.</param>
	/// <param name="legalHoldService">The authority asked whether a hold still blocks the request.</param>
	/// <param name="logger">The logger.</param>
	public ErasureHoldReevaluationProcessor(
		IErasureStore store,
		ILegalHoldService legalHoldService,
		ILogger<ErasureHoldReevaluationProcessor> logger)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_legalHoldService = legalHoldService ?? throw new ArgumentNullException(nameof(legalHoldService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<int> ReevaluateDeferredErasuresAsync(CancellationToken cancellationToken)
	{
		var queryStore = (IErasureBackgroundQueryStore?)_store.GetService(typeof(IErasureBackgroundQueryStore))
			?? throw new InvalidOperationException(
				"The erasure store must expose IErasureBackgroundQueryStore for background hold re-evaluation. " +
				"Implement its estate-wide hold-deferred discovery; consumer listing cannot substitute for it.");

		var deferred = new List<ErasureStatus>();
		for (var page = 1; ; page++)
		{
			var batch = await queryStore.GetHoldDeferredRequestsAsync(
				page,
				PageSize,
				cancellationToken).ConfigureAwait(false);

			deferred.AddRange(batch);

			if (batch.Count < PageSize)
			{
				break;
			}
		}

		if (deferred.Count == 0)
		{
			return 0;
		}

		var resumed = 0;
		foreach (var request in deferred.DistinctBy(static row => row.RequestId))
		{
			cancellationToken.ThrowIfCancellationRequested();

			IDisposable tenantScope;
			try
			{
				tenantScope = ErasureRequestTenantScope.Begin(request);
			}
#pragma warning disable CA1031 // A malformed recorded owner must not stop other requests.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				LogReevaluationFailed(request.RequestId, ex);
				continue;
			}

			using var ownedScope = tenantScope;
			try
			{
				var holdCheck = await _legalHoldService.CheckHoldsByHashAsync(
					request.DataSubjectIdHash, request.TenantId, cancellationToken).ConfigureAwait(false);

				// A PARTIAL hold blocks this erasure exactly as a full one does, because neither mechanism
				// the executor runs can exclude a category from destruction -- the subject key is scoped to
				// the subject and the event-store pass is whole-aggregate. Treating partial as "no longer
				// blocked" here would hand the executor a request it must immediately refuse again.
				if (holdCheck.ErasureBlocked || holdCheck.IsPartiallyBlocked)
				{
					continue;
				}

				// NO CLAIM, and null is the STRICTER argument rather than an omission. This is a SUPERVISOR: it
				// cannot obtain an identity, because the identity is minted inside the executor and never comes
				// back out. Passing null says "do this only if nobody is working on the request", which is exactly
				// what a supervisor means -- so the write is refused while a live claim exists and applies once
				// that claim lapses.
				if (await _store.UpdateStatusAsync(
						request.RequestId,
						ErasureRequestStatus.Scheduled,
						expectedStatus: null,
						errorMessage: null,
						claimIdentity: null,
						cancellationToken).ConfigureAwait(false) == ErasureWriteOutcome.Applied)
				{
					resumed++;
					LogResumed(request.RequestId);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // One request's failure must not stop the others from being re-evaluated.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// The request stays deferred, which is revisited on the next call.
				LogReevaluationFailed(request.RequestId, ex);
			}
		}

		LogPassCompleted(deferred.Count, resumed);
		return resumed;
	}

	[LoggerMessage(
		ComplianceEventId.ErasureHoldReevaluationResumed,
		LogLevel.Information,
		"Erasure request {RequestId} was deferred by a legal hold that no longer blocks it, so it is returned to the scheduled state; the executor re-checks holds before erasing anything")]
	private partial void LogResumed(Guid requestId);

	[LoggerMessage(
		ComplianceEventId.ErasureHoldReevaluationFailed,
		LogLevel.Error,
		"Re-evaluating the legal hold on deferred erasure request {RequestId} failed; it remains deferred and is retried on the next pass")]
	private partial void LogReevaluationFailed(Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureHoldReevaluationPassCompleted,
		LogLevel.Information,
		"Erasure hold re-evaluation pass examined {Deferred} deferred request(s) and returned {Resumed} to the scheduled state")]
	private partial void LogPassCompleted(int deferred, int resumed);
}
