// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// Returns erasure requests that a legal hold deferred to the executable state once no hold blocks them.
/// </summary>
/// <remarks>
/// <para>
/// A legal hold lawfully defers an erasure rather than failing it, so a deferred request is not finished
/// and the controller's obligation to erase is not discharged. Without this pass the deferral is
/// permanent: nothing re-examines the hold, so releasing or letting a hold lapse leaves the request
/// deferred forever with no signal to anyone.
/// </para>
/// <para>
/// The pass grants ELIGIBILITY, never permission to erase. It returns a request to the scheduled state
/// and nothing more; the executor re-checks holds after it claims the request, and again before each
/// irreversible destruction, so a hold created between this pass and the execution costs one wasted
/// claim cycle rather than erasing held data.
/// </para>
/// <para>
/// A host without a background service -- a serverless function, a scheduled job -- calls this directly,
/// which is why it is part of the consumer surface rather than internal.
/// </para>
/// </remarks>
public interface IErasureHoldReevaluationProcessor
{
	/// <summary>
	/// Re-examines every erasure request a legal hold has deferred and returns the ones no longer
	/// blocked to the scheduled state.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of requests returned to the scheduled state by this pass.</returns>
	Task<int> ReevaluateDeferredErasuresAsync(CancellationToken cancellationToken);
}
