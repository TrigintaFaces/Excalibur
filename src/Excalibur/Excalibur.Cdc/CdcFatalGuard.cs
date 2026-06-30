// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Cdc;

/// <summary>
/// The single, shared, pure decision point for a CDC consume loop's checkpoint-advance / stop
/// behavior on every iteration. Every provider's <c>StartAsync</c> loop routes its fatal-vs-transient
/// decision through <see cref="Decide"/>.
/// </summary>
/// <remarks>
/// <para>
/// The FR-B2 / safety invariant — <em>a fatal (or transient) fault never advances the durable
/// checkpoint past the unprocessed change</em> — is enforced by the idiom native
/// to each provider class (see <see cref="CdcFatalDecision"/>): <b>poll-batch</b> providers (Cosmos,
/// DynamoDb) gate the durable advance literally on <see cref="CdcFatalDecision.AdvanceCheckpoint"/> at a
/// site reached on both the success and the captured-fault path; <b>streaming</b> providers (Postgres,
/// Mongo) enforce it by confirm-site placement (the fault unwinds before the commit/invalidation confirm
/// is reachable). A field-gate on a streaming confirm site would be vacuous, so it is not used there.
/// </para>
/// <para>
/// <see cref="Decide"/> itself is pure and deterministic (no I/O), so the decision arms are directly and
/// non-vacuously unit-testable. The end-to-end durability invariant is regression-locked by the
/// poll-batch field-gate mutant test (Cosmos/DynamoDb) and the non-skipped real-infra restart-redelivery
/// test (Postgres/Mongo).
/// </para>
/// </remarks>
public static class CdcFatalGuard
{
    /// <summary>
    /// Decides the loop's action for one iteration from the outcome of processing a change/batch.
    /// </summary>
    /// <param name="exception">
    /// The exception raised while processing the change/batch, or <see langword="null"/> when the
    /// iteration succeeded.
    /// </param>
    /// <param name="classifier">
    /// The optional failure classifier deciding whether a fault is fatal (non-retryable); when
    /// <see langword="null"/>, the built-in <see cref="CdcFatalClassifier"/> defaults apply.
    /// </param>
    /// <returns>
    /// On success (<paramref name="exception"/> is <see langword="null"/>) → advance + continue.
    /// On a fatal fault → do NOT advance, stop. On a transient fault → do NOT advance, reconnect.
    /// </returns>
    public static CdcFatalDecision Decide(Exception? exception, IMessageFailureClassifier? classifier)
    {
        // Clean success — the only path that may advance the durable checkpoint.
        // (advanceCheckpoint: true, stop: false)
        if (exception is null)
        {
            return new CdcFatalDecision(true, false);
        }

        // A fault occurred: NEVER advance past it. Fatal → stop loudly (false, true);
        // transient → do not stop, the loop reconnects and retries from the un-advanced checkpoint (false, false).
        return CdcFatalClassifier.IsFatal(exception, classifier)
            ? new CdcFatalDecision(false, true)
            : new CdcFatalDecision(false, false);
    }
}
