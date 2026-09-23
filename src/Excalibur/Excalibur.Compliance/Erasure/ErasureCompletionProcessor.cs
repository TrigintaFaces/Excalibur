// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Default <see cref="IErasureCompletionProcessor"/>: finds every request in
/// <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/> and asks <see cref="ErasureService"/> to confirm it.
/// </summary>
/// <remarks>
/// The request set is read in full before any request is confirmed. Confirming a request removes it from the
/// status filter, so confirming while paging would shift later pages and skip requests; reading first means a
/// single call visits every request that was waiting when it started.
/// </remarks>
internal sealed partial class ErasureCompletionProcessor : IErasureCompletionProcessor
{
	private const int PageSize = 500;

	private readonly ErasureService _erasureService;
	private readonly IErasureStore _store;
	private readonly IErasureVerificationService? _verifier;
	private readonly ILogger<ErasureCompletionProcessor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureCompletionProcessor"/> class.
	/// </summary>
	/// <param name="erasureService">The erasure service that performs confirmation and issues the certificate.</param>
	/// <param name="store">The erasure store to read waiting requests from.</param>
	/// <param name="verifier">
	/// The verification service used to ask the provider whether a key is destroyed, or <see langword="null"/>
	/// when none is registered -- in which case waiting requests are reported and left waiting.
	/// </param>
	/// <param name="logger">The logger.</param>
	public ErasureCompletionProcessor(
		ErasureService erasureService,
		IErasureStore store,
		IErasureVerificationService? verifier,
		ILogger<ErasureCompletionProcessor> logger)
	{
		_erasureService = erasureService ?? throw new ArgumentNullException(nameof(erasureService));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_verifier = verifier;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<int> CompletePendingErasuresAsync(CancellationToken cancellationToken)
	{
		var queryStore = (IErasureQueryStore?)_store.GetService(typeof(IErasureQueryStore))
			?? throw new InvalidOperationException(
				"The erasure store does not support query operations, so requests awaiting key destruction cannot be found.");

		var waiting = new List<Guid>();
		for (var page = 1; ; page++)
		{
			var batch = await queryStore.ListRequestsAsync(
				ErasureRequestStatus.AwaitingKeyDestruction,
				tenantId: null,
				fromDate: null,
				toDate: null,
				page,
				PageSize,
				cancellationToken).ConfigureAwait(false);

			waiting.AddRange(batch.Select(static r => r.RequestId));

			if (batch.Count < PageSize)
			{
				break;
			}
		}

		if (waiting.Count == 0)
		{
			return 0;
		}

		// No verifier means no way to ask the provider. That is a configuration the host must fix, and it is said
		// out loud on every pass rather than resolved by completing anything unconfirmed.
		if (_verifier is null)
		{
			LogVerifierMissing(waiting.Count);
			return 0;
		}

		var completed = 0;
		foreach (var requestId in waiting.Distinct())
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (await _erasureService.ConfirmKeyDestructionAsync(requestId, _verifier, cancellationToken)
					.ConfigureAwait(false))
				{
					completed++;
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
#pragma warning disable CA1031 // One request's failure must not stop the others from being confirmed.
			catch (Exception ex)
#pragma warning restore CA1031
			{
				// The request is left in AwaitingKeyDestruction, which is revisited on the next call.
				LogConfirmFailed(requestId, ex);
			}
		}

		LogPassCompleted(waiting.Count, completed);
		return completed;
	}

	[LoggerMessage(
		ComplianceEventId.ErasureCompletionVerifierMissing,
		LogLevel.Warning,
		"{Count} erasure request(s) are awaiting key destruction, but no IErasureVerificationService is registered, so their key destruction cannot be confirmed and they cannot complete. Register one with AddErasureVerificationService().")]
	private partial void LogVerifierMissing(int count);

	[LoggerMessage(
		ComplianceEventId.ErasureCompletionConfirmFailed,
		LogLevel.Error,
		"Confirming key destruction for erasure request {RequestId} failed; it remains awaiting key destruction and is retried on the next pass")]
	private partial void LogConfirmFailed(Guid requestId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.ErasureCompletionPassCompleted,
		LogLevel.Information,
		"Erasure completion pass checked {Waiting} request(s) awaiting key destruction and completed {Completed}")]
	private partial void LogPassCompleted(int waiting, int completed);
}
