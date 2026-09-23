// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Drives erasure requests that are waiting on a key-management provider's key destruction to a terminal
/// state, on demand.
/// </summary>
/// <remarks>
/// <para>
/// On key stores that cannot destroy key material immediately -- AWS KMS for a KMS-generated key, or Azure
/// Key Vault when the vault's purge protection or the credential's permissions prevent an immediate purge --
/// an executed erasure enters <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/>. Nothing is left for
/// the framework to do except find out, later, whether the provider has actually destroyed the key.
/// </para>
/// <para>
/// This processor is that step. Each call asks the key-management provider, through
/// <see cref="IErasureVerificationService.VerifyKeyDeletionAsync"/>, whether every key of every waiting
/// request is gone. A request whose keys are all confirmed destroyed moves to
/// <see cref="ErasureRequestStatus.Completed"/> and receives its completion certificate. A request with any
/// key still recoverable is left exactly as it was, to be asked about again on a later call. It never
/// deletes, schedules, or re-executes anything: a key that is scheduled for destruction cannot be scheduled
/// again, and the request's other erasure work was already done when it was executed.
/// </para>
/// <para>
/// <b>The framework owns the logic; the host owns the trigger.</b> The optional erasure scheduler
/// background service calls this on every polling cycle. A host without a background service -- a serverless
/// function, for example -- calls it from whatever trigger it has: a timer, a scheduled job, or an
/// administrative endpoint. How often it is called only affects how soon a request reaches
/// <see cref="ErasureRequestStatus.Completed"/> after the provider destroys its key; calling it early is safe,
/// because a key the provider still holds is never reported as destroyed.
/// </para>
/// <para>
/// Confirmation requires an <see cref="IErasureVerificationService"/> to be registered. Without one, waiting
/// requests are reported in the log and left waiting; they are never completed unconfirmed.
/// </para>
/// </remarks>
public interface IErasureCompletionProcessor
{
	/// <summary>
	/// Checks every erasure request awaiting key destruction and completes each one whose keys the provider
	/// confirms are destroyed.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The number of requests moved to <see cref="ErasureRequestStatus.Completed"/> by this call.</returns>
	Task<int> CompletePendingErasuresAsync(CancellationToken cancellationToken);
}
