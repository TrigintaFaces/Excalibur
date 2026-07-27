// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows;

/// <summary>
/// The determinism boundary for a durable workflow body: the only legal surface for non-deterministic
/// operations inside a workflow.
/// </summary>
/// <remarks>
/// Non-deterministic operations MUST flow through this context. The replay executor records each call to
/// the workflow journal and, on replay, returns the previously recorded result deterministically rather
/// than re-performing the operation. A workflow body that performs non-deterministic work directly instead
/// of through this context is non-deterministic and will diverge on replay. The current surface journals
/// activity invocation; deterministic time, identifier generation, durable timers, and external signals are
/// obtained inside a journaled activity until dedicated context primitives are delivered.
/// <para>
/// A workflow body invokes this context <b>sequentially</b> on a single logical thread: await each context
/// call before starting the next. Concurrent or re-entrant calls (for example awaiting several
/// <see cref="CallActivityAsync{TResult}"/> in parallel) are rejected, because interleaved calls would make
/// journal replay non-deterministic.
/// </para>
/// </remarks>
public interface IWorkflowContext
{
	/// <summary>
	/// Schedules an activity for at-least-once execution and awaits its result. The scheduling and completion
	/// are journaled, so on replay the recorded result is returned without re-invoking the activity.
	/// </summary>
	/// <typeparam name="TResult">The activity result type.</typeparam>
	/// <param name="activityName">The registered activity name.</param>
	/// <param name="input">The activity input.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The activity result, replayed deterministically from the journal.</returns>
	ValueTask<TResult> CallActivityAsync<TResult>(string activityName, object input, CancellationToken cancellationToken);

	/// <summary>
	/// Creates a durable timer that completes after the specified delay, surviving process restarts and
	/// firing exactly once.
	/// </summary>
	/// <remarks>
	/// The timer's creation and firing are journaled, so the due time is anchored to the durably recorded
	/// creation instant rather than wall-clock at each resume: a process that crashes while waiting resumes
	/// the same timer and completes at the original due time. The wait is driven by the workflow's controllable
	/// time source, and the firing transition is an atomic journal claim, so concurrent resumes complete the
	/// timer exactly once. Use this instead of <c>Task.Delay</c> or a wall-clock deadline inside a workflow
	/// body, both of which are non-deterministic and do not survive a restart.
	/// </remarks>
	/// <param name="delay">The duration to wait before the timer fires. A non-positive delay fires immediately.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>A task that completes when the durable timer fires.</returns>
	ValueTask CreateTimerAsync(TimeSpan delay, CancellationToken cancellationToken);

	/// <summary>
	/// Returns a deterministic UTC timestamp for use inside a workflow body, journaled so replay reproduces
	/// the original instant rather than reading wall-clock again.
	/// </summary>
	/// <remarks>
	/// Use this instead of <see cref="DateTimeOffset.UtcNow"/> or <see cref="DateTime.UtcNow"/> inside a
	/// workflow body: reading the ambient clock directly is non-deterministic and diverges on replay. The read
	/// is recorded to the journal on first execution and, on replay, the recorded instant is returned without
	/// re-reading the clock — so a value derived from "now" (a deadline, an audit stamp) survives a restart
	/// unchanged.
	/// </remarks>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The deterministic UTC instant, replayed from the journal on resume.</returns>
	ValueTask<DateTimeOffset> UtcNowAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Returns a deterministic new <see cref="Guid"/> for use inside a workflow body, journaled so replay
	/// reproduces the original identifier rather than generating a new one.
	/// </summary>
	/// <remarks>
	/// Use this instead of <see cref="Guid.NewGuid"/> inside a workflow body: generating an identifier directly
	/// is non-deterministic and diverges on replay. The generated value is recorded to the journal on first
	/// execution and, on replay, the recorded identifier is returned — so an id assigned to a business record
	/// stays stable across a restart. This is a workflow business identifier, not cryptographic key material.
	/// </remarks>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The deterministic identifier, replayed from the journal on resume.</returns>
	ValueTask<Guid> NewGuidAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Suspends the workflow until an external signal of the given name is delivered, then returns its
	/// payload. Signals are delivered via <see cref="IWorkflowExecutor.SignalAsync"/>.
	/// </summary>
	/// <remarks>
	/// The first delivered, not-yet-consumed signal with a matching <paramref name="signalName"/> is
	/// consumed in inbox-arrival order. Consumption is journaled at this call's deterministic step position,
	/// so on replay the recorded signal payload is returned without re-consuming from the inbox. Delivery is
	/// exactly-once into the journal (dedup on the producer-supplied signal id); a workflow that awaits the
	/// same signal name more than once consumes successive matching signals in arrival order.
	/// </remarks>
	/// <typeparam name="TResult">The signal payload type.</typeparam>
	/// <param name="signalName">The signal name to await.</param>
	/// <param name="cancellationToken">A token to observe for cancellation.</param>
	/// <returns>The signal payload, replayed deterministically from the journal on resume.</returns>
	ValueTask<TResult> WaitForSignalAsync<TResult>(string signalName, CancellationToken cancellationToken);
}
