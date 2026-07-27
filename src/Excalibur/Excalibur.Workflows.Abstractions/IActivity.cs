// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// An at-least-once unit of side-effecting work invoked from a workflow through
/// <see cref="IWorkflowContext.CallActivityAsync{TResult}"/>.
/// </summary>
/// <remarks>
/// Activities carry the side effects a workflow body must not perform directly. Because a crash between an
/// activity's execution and the journaling of its completion causes the activity to run again on replay,
/// every activity MUST be idempotent — executing it twice with the same input must not double-apply its
/// effect.
/// </remarks>
/// <typeparam name="TInput">The activity input type.</typeparam>
/// <typeparam name="TOutput">The activity output type.</typeparam>
public interface IActivity<TInput, TOutput>
{
	/// <summary>
	/// Executes the activity for the given input. Must be idempotent to tolerate at-least-once replay.
	/// </summary>
	/// <param name="input">The activity input.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The activity result.</returns>
	ValueTask<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
