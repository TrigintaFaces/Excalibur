// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Marker interface for messages that should be executed in the background.
/// </summary>
/// <remarks>
/// <para>
/// Messages implementing this interface are handed to a background worker, and the dispatch returns immediately with a successful
/// result whose disposition is <see cref="MessageDisposition.AcceptedForBackgroundExecution"/>: the work is accepted and pending, and
/// the handler has not run yet. Messages expecting typed results cannot implement this interface as background execution does not
/// support returning values to the caller.
/// </para>
/// <para>
/// A dispatch whose cancellation token is already cancelled is not accepted: the handler never runs and the result is cancelled.
/// Once accepted, the work no longer follows the caller's token, which is often request-scoped and fires when the response carrying
/// the acceptance completes.
/// </para>
/// <para>
/// Background execution is decoupled, not durable. A graceful host shutdown waits for in-flight background work, bounded by
/// <c>HostOptions.ShutdownTimeout</c>; work still running when that budget elapses is signalled to cancel, and an error is logged for it.
/// An abrupt termination still loses in-flight work, which is inherent to running in-process. Work that must survive a crash or restart
/// belongs in the outbox.
/// </para>
/// <para>
/// What happens when background work fails is a host-level policy, not a property of the message: see
/// <see cref="Options.Threading.BackgroundExecutionOptions.ExceptionBehavior"/>.
/// </para>
/// </remarks>
public interface IExecuteInBackground
{
}
