// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.StateMachine;

/// <summary>
/// Saga state for a <see cref="ProcessManager{TData}"/>: the consumer's data, plus the position the
/// state machine has reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>The abstract value of a process manager is the pair (data, position), and BOTH are saga state.</b>
/// A process manager is constructed fresh for every delivered message, so a position held anywhere but
/// the persisted state does not survive to the next message. Requiring it here is what makes it
/// survive — there is no member to override and no convention to remember.
/// </para>
/// <para>
/// <b>Why this type exists at all, since the position used to live in a field.</b> It did, and it was
/// lost on every message. The base class held <c>_currentState</c>, initialised to <c>"Initial"</c>,
/// and exposed a <c>protected virtual CurrentStateName</c> that the documentation told consumers to
/// override so their position would be persisted. That mechanism could not work: the framework WROTE
/// the virtual property on transition and never READ it back, so a consumer who followed the
/// instruction exactly got a value faithfully stored and never loaded, and their saga still resumed at
/// <c>"Initial"</c>. A multi-state process manager was therefore broken on its second message, and the
/// class documentation described the persistence as automatic.
/// </para>
/// <para>
/// The remedy is not a check that the override was written — that would certify compliance with a
/// ritual which does not restore the invariant. It is to remove the choice: the position lives in the
/// state, there is one representation of it, and the type system requires it.
/// </para>
/// <para>
/// <b>Consumer obligation:</b> derive your saga data from this type instead of <see cref="SagaState"/>.
/// Nothing else changes; a single-state process manager simply carries an unused string.
/// </para>
/// </remarks>
public abstract class ProcessManagerState : SagaState
{
    /// <summary>
    /// Gets or sets the state-machine position this saga has reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set by <see cref="ProcessManager{TData}.TransitionTo"/> and read by the handler lookup. It is
    /// persisted and loaded with the rest of the saga state, which is the whole point of it living
    /// here.
    /// </para>
    /// <para>
    /// It is settable because the store materialises it on load, and because a consumer migrating
    /// existing saga rows may need to seed it. Prefer <c>TransitionTo</c> in saga code: that is the
    /// only operation that checks the target state exists and runs the exit and entry hooks, and
    /// assigning this property directly bypasses both.
    /// </para>
    /// </remarks>
    /// <value>The current state name; <c>"Initial"</c> for a saga that has not yet transitioned.</value>
    public string CurrentStateName { get; set; } = "Initial";
}
