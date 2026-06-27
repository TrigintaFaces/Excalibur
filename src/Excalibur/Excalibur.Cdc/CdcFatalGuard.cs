// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Cdc;

/// <summary>
/// The single, shared, pure decision point for a CDC consume loop's checkpoint-advance / stop /
/// reconnect behavior on every iteration. Every provider's <c>StartAsync</c> loop routes through
/// <see cref="Decide"/> and gates ALL checkpoint-advance on the returned
/// <see cref="CdcFatalDecision.AdvanceCheckpoint"/>.
/// </summary>
/// <remarks>
/// <para>
/// This makes the FR-B2 / ADR-338 safety invariant — <em>a fatal (or transient) fault never advances
/// the durable checkpoint past the unprocessed change</em> — <strong>structurally inexpressible</strong>
/// to violate: the loop has no un-gated advance, and a fault never returns
/// <see cref="CdcFatalDecision.AdvanceCheckpoint"/> = <see langword="true"/> (bd-pxhqri, SA ruling 17124).
/// </para>
/// <para>
/// Pure and deterministic (no I/O, no real infrastructure), so the invariant is directly and
/// non-vacuously unit-testable: mutating any decision arm flips a bound assertion to RED.
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
        // (advanceCheckpoint: true, stop: false, reconnect: false)
        if (exception is null)
        {
            return new CdcFatalDecision(true, false, false);
        }

        // A fault occurred: NEVER advance past it. Fatal → stop loudly (false, true, false);
        // transient → reconnect and retry from the un-advanced checkpoint (false, false, true).
        return CdcFatalClassifier.IsFatal(exception, classifier)
            ? new CdcFatalDecision(false, true, false)
            : new CdcFatalDecision(false, false, true);
    }
}
