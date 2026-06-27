// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Classifies a message-processing failure to decide how the pipeline should react to it.
/// </summary>
/// <remarks>
/// <para>
/// This is the single, shared failure taxonomy consumed by every resilience-aware component
/// (retry middleware, retry policies, the outbox/inbox processors, and the change-data-capture
/// processors) so that the decision "retry, or give up and dead-letter" is made consistently
/// regardless of which component encountered the failure.
/// </para>
/// <para>
/// The classification is a <em>value</em> result, not an exception: classifying a failure is a
/// common, expected operation on the hot path, so it never throws (cf. the predicate-outcome model
/// used by resilience libraries such as Polly).
/// </para>
/// </remarks>
public enum MessageFailureKind
{
    /// <summary>
    /// A transient failure that is expected to succeed on a later attempt — for example a network
    /// timeout, a temporary transport/database outage, or resource contention. The normal backoff
    /// retry policy applies.
    /// </summary>
    Transient = 0,

    /// <summary>
    /// A permanent failure that will not succeed on retry but is a property of the operation rather
    /// than the message itself — for example a validation, authorization, or programming error.
    /// Retrying is futile; the message is dead-lettered with the matching reason.
    /// </summary>
    Permanent = 1,

    /// <summary>
    /// A poison message that can never be processed because the message itself is defective — for
    /// example it cannot be deserialized, names an unknown type, or has no registered handler.
    /// Such a message is dead-lettered immediately, without consuming retry attempts, so it cannot
    /// block the pipeline.
    /// </summary>
    Poison = 2,
}
