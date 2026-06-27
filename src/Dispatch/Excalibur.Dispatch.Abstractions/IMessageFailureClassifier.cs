// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Classifies an exception raised while processing a message into a <see cref="MessageFailureKind"/>,
/// so that resilience-aware components share one consistent "retry vs. dead-letter" decision.
/// </summary>
/// <remarks>
/// <para>
/// A single classifier is consumed by the retry middleware, retry policies, the outbox/inbox
/// processors, and the change-data-capture processors. Centralising the taxonomy here guarantees a
/// transient transport blip is retried — and a poison message is dead-lettered — identically across
/// every path, rather than each component re-deriving its own divergent rules.
/// </para>
/// <para>
/// Implementations MUST be deterministic, free of side effects, and safe to call concurrently. They
/// MUST NOT throw on the hot path: classification returns a value for every input (an unrecognised
/// failure resolves to <see cref="MessageFailureKind.Transient"/> so a bounded retry — never an
/// infinite loop or a silent drop — is the safe default).
/// </para>
/// </remarks>
public interface IMessageFailureClassifier
{
    /// <summary>
    /// Classifies the supplied exception into a <see cref="MessageFailureKind"/>.
    /// </summary>
    /// <param name="exception">The exception that caused the message-processing failure.</param>
    /// <returns>
    /// The failure classification: <see cref="MessageFailureKind.Transient"/> for a retryable
    /// failure, <see cref="MessageFailureKind.Permanent"/> for a non-retryable operation failure, or
    /// <see cref="MessageFailureKind.Poison"/> for a defective message that must be dead-lettered
    /// immediately.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    MessageFailureKind Classify(Exception exception);
}
