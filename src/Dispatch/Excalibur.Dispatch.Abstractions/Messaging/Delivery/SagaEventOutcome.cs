// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Whether a saga actually acted on an event it was given.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because a saga that declines is indistinguishable from one that acts, if the only
/// signal is a bare <c>Task</c>.</b> A handler guarded by a condition returns without doing anything
/// when the condition is false. The coordinator could not see that, so it recorded the event as
/// processed and persisted the record — permanently retiring a message that no handler had touched.
/// A message arriving before its guard was satisfiable was never redelivered.
/// </para>
/// <para>
/// The remedy is not a log line: a log is observable to an operator reading it afterwards, never to
/// the caller deciding what to do next. The refusal has to be a value the caller can branch on, which
/// is what this is.
/// </para>
/// <para>
/// It is deliberately two states and not three. "The saga has no handler for this type at all" is a
/// different question, answered before delivery by <c>HandlesEvent</c>, and folding it in here would
/// let a routing miss and a deliberate decline share one value again — which is the conflation that
/// produced the defect.
/// </para>
/// </remarks>
public enum SagaEventOutcome
{
    /// <summary>
    /// The saga declined to act: a handler was found, and its condition was not met.
    /// </summary>
    /// <remarks>
    /// The event is NOT recorded as processed, so it remains deliverable. This is the correct outcome
    /// for a message that arrives before the saga is ready for it — it is deferred, not discarded.
    /// It is the default so that a handler which somehow reports nothing is treated as not having
    /// acted, which is the safe direction: the cost is a redelivery, not a lost message.
    /// </remarks>
    Declined = 0,

    /// <summary>
    /// The saga acted on the event.
    /// </summary>
    /// <remarks>
    /// The event is recorded as processed and that record is persisted with the state the handler
    /// produced, so a redelivery inside the dedup window will not run it again.
    /// </remarks>
    Handled = 1,
}
