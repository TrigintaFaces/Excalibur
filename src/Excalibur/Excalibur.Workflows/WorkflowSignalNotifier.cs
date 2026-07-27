// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Workflows;

/// <summary>
/// In-process wake coordination for signal delivery to a <b>running</b> workflow instance: a body parked on
/// <c>WaitForSignalAsync</c> awaits a per-instance gate that <see cref="IWorkflowExecutor.SignalAsync"/>
/// releases after admitting a signal to the inbox.
/// </summary>
/// <remarks>
/// This wakes only an instance whose executor is currently running in this process (the durable-timer
/// in-process model). Waking a suspended instance across a restart requires a durable runnable-queue, which
/// is a separate substrate. Callers must capture <see cref="WaitAsync"/> <em>before</em> draining the inbox
/// so a signal admitted between the drain and the await is not a lost wakeup.
/// </remarks>
internal sealed class WorkflowSignalNotifier
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns a task that completes when the next signal is delivered for the instance. Capture this before
    /// draining the inbox, then await it after finding no match, to avoid a lost wakeup.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    /// <returns>A task released by the next <see cref="Notify"/> for the instance.</returns>
    public Task WaitAsync(string instanceId) =>
        _gates.GetOrAdd(
            instanceId,
            static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    /// <summary>
    /// Releases the current waiter (if any) for the instance. The next <see cref="WaitAsync"/> observes a
    /// fresh, uncompleted gate.
    /// </summary>
    /// <param name="instanceId">The workflow instance identifier.</param>
    public void Notify(string instanceId)
    {
        if (_gates.TryRemove(instanceId, out var gate))
        {
            gate.TrySetResult();
        }
    }
}
