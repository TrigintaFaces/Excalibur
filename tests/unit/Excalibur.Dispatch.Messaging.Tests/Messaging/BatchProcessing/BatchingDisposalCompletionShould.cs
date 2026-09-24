// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Middleware.Batch;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestFakes;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.BatchProcessing;

/// <summary>
/// Every message the batching middleware accepts must have its awaited task completed — by processing, by
/// the caller's own cancellation, or by disposal — and never left unfinished.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> <c>InvokeAsync</c> handed the caller a <see cref="TaskCompletionSource{TResult}"/>
/// and awaited its task with no timeout, no cancellation registration and no completion on disposal. The
/// batch loop is not obliged to complete what it discards, and it discards on two ORDINARY paths: it drops
/// an item whose token is already cancelled when the batch is assembled, and it abandons items it has read
/// but not yet flushed when disposal cancels it. In both the completion source was simply never completed,
/// so the caller awaited a task nothing would ever finish — a permanent hang, holding the request thread's
/// continuation and everything rooted by it.
/// </para>
/// <para>
/// <b>WHY THIS IS STEADY STATE, NOT A SHUTDOWN RACE.</b> The middleware is registered SCOPED, so disposal
/// runs at the end of EVERY scope — on every request, concurrently, for the life of the process. And the
/// cancelled-item path needs no disposal at all: an ordinary client disconnect cancels the request token,
/// and that caller then parks forever. Neither interleaving is exotic.
/// </para>
/// <para>
/// <b>WHY THE FIX IS TWO REGISTRATIONS AND NOT A PROTOCOL.</b> The completion source lives in the
/// middleware and the discard happens inside the generic batch processor, where the item type is opaque —
/// so the component that drops an item cannot complete it. Rather than invent a discard-callback protocol
/// between the two, the middleware registers on both tokens and lets <c>TrySet*</c> arbitrate: whichever
/// outcome happens first wins and the others are no-ops. That is exactly-once completion without the two
/// components having to agree on anything, using the primitive the BCL already provides.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Priority", "1")]
public sealed class BatchingDisposalCompletionShould
{
	/// <summary>A window long enough that nothing flushes on its own, so the batch genuinely sits pending.</summary>
	private static readonly TimeSpan NeverFlushes = TimeSpan.FromMinutes(5);

	/// <summary>How long a completed-or-hung decision waits before calling it a hang.</summary>
	private static readonly TimeSpan HangThreshold = TimeSpan.FromSeconds(5);

	/// <summary>
	/// SAFETY — the headline defect. A caller waiting in a batch must be released when the scope ends.
	/// </summary>
	[Fact]
	public async Task CompleteAWaitingCaller_WhenTheMiddlewareIsDisposedWhileTheBatchIsStillPending()
	{
		var middleware = CreateMiddleware();

		var inFlight = middleware.InvokeAsync(
			new FakeDispatchMessage(),
			new FakeMessageContext(),
			NeverCompletes,
			CancellationToken.None).AsTask();

		// The item is now sitting in a batch that will not flush for five minutes. Ending the scope must
		// release the caller rather than abandon them.
		await middleware.DisposeAsync().ConfigureAwait(false);

		var settled = await Task.WhenAny(inFlight, Task.Delay(HangThreshold)).ConfigureAwait(false);

		settled.ShouldBeSameAs(
			inFlight,
			"disposal must complete every accepted message's task. A task still running after the disposer "
			+ "has finished is the permanent hang: nothing else will ever complete it");

		// The outcome must also be INTELLIGIBLE. A caller told only "cancelled" cannot tell a shutdown from
		// their own timeout, and the two want different handling.
		var thrown = await Should.ThrowAsync<ObjectDisposedException>(inFlight).ConfigureAwait(false);
		thrown.Message.ShouldContain(
			"not been sent",
			Case.Sensitive,
			"the caller must be told the message was accepted but not dispatched, because that decides "
			+ "whether retrying is safe");
	}

	/// <summary>
	/// SAFETY. Needs no disposal at all — this is an ordinary client disconnect.
	/// </summary>
	[Fact]
	public async Task CompleteAWaitingCaller_WhenTheirOwnTokenIsCancelledWhileTheBatchIsPending()
	{
		await using var middleware = CreateMiddleware();
		using var callerCancellation = new CancellationTokenSource();

		var inFlight = middleware.InvokeAsync(
			new FakeDispatchMessage(),
			new FakeMessageContext(),
			NeverCompletes,
			callerCancellation.Token).AsTask();

		await callerCancellation.CancelAsync().ConfigureAwait(false);

		var settled = await Task.WhenAny(inFlight, Task.Delay(HangThreshold)).ConfigureAwait(false);

		settled.ShouldBeSameAs(
			inFlight,
			"a caller who cancels must be released. The batch assembly DROPS an item whose token is already "
			+ "cancelled, so nothing downstream will ever complete it — the caller's own cancellation is the "
			+ "only thing that can");

		_ = await Should.ThrowAsync<OperationCanceledException>(inFlight).ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY. Refusing after disposal, rather than silently building a processor nothing will dispose.
	/// </summary>
	/// <remarks>
	/// <b>THIS ARM ASSERTS THE <see cref="ObjectDisposedException.ObjectName"/>, AND THAT IS THE WHOLE ARM
	/// — WITHOUT IT, IT IS VACUOUS.</b> An earlier version asserted only the exception TYPE and passed with
	/// the disposal guard deleted, because the disposed <see cref="CancellationTokenSource"/> throws
	/// <see cref="ObjectDisposedException"/> of its own accord when the completion registrations touch its
	/// token. Same type, different origin, and the weaker assertion could not tell them apart — a healthy
	/// instrument answering a question next to the one that mattered. Naming the object discriminates:
	/// only this middleware's own guard reports this type, and that guard is the one statement that runs
	/// BEFORE <c>GetOrAdd</c> and therefore the only thing that actually prevents the resurrection.
	/// </remarks>
	[Fact]
	public async Task RefuseANewDispatchAfterDisposal_RatherThanResurrectingAProcessor()
	{
		var middleware = CreateMiddleware();
		await middleware.DisposeAsync().ConfigureAwait(false);

		var thrown = await Should.ThrowAsync<ObjectDisposedException>(
			async () => await middleware.InvokeAsync(
				new FakeDispatchMessage(),
				new FakeMessageContext(),
				NeverCompletes,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Contains rather than equals: ObjectDisposedException.ThrowIf(_, this) reports the FULL type name.
		thrown.ObjectName.ShouldContain(
			nameof(UnifiedBatchingMiddleware),
			Case.Sensitive,
			"the refusal must come from this middleware's own disposal guard, which runs before GetOrAdd. "
			+ "An ObjectDisposedException naming CancellationTokenSource means the guard did NOT fire and "
			+ "the caller was refused incidentally, downstream of where the processor would be built");
	}

	/// <summary>
	/// LIVENESS, and without it the three arms above are worthless. A middleware that refused or cancelled
	/// EVERYTHING would satisfy all of them; this proves the ordinary path still dispatches and returns the
	/// handler's own result.
	/// </summary>
	[Fact]
	public async Task StillDispatchAMessageNormally_WhenNothingIsCancelledOrDisposed()
	{
		await using var middleware = CreateMiddleware(flushImmediately: true);

		var result = await middleware.InvokeAsync(
			new FakeDispatchMessage(),
			new FakeMessageContext(),
			static (_, _, _) => ValueTask.FromResult<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		result.Succeeded.ShouldBeTrue("the undisturbed path must still deliver the handler's result");
	}

	private static UnifiedBatchingMiddleware CreateMiddleware(bool flushImmediately = false) =>
		new(
			Microsoft.Extensions.Options.Options.Create(new UnifiedBatchingOptions
			{
				MaxBatchSize = flushImmediately ? 1 : 1024,
				MaxBatchDelay = flushImmediately ? TimeSpan.FromMilliseconds(10) : NeverFlushes,
			}),
			NullLogger<UnifiedBatchingMiddleware>.Instance,
			NullLoggerFactory.Instance);

	/// <summary>
	/// A downstream that never returns, so the only thing that can complete the caller is the middleware.
	/// If the arm passes because this ran, the arm is measuring the wrong thing.
	/// </summary>
	private static async ValueTask<IMessageResult> NeverCompletes(
		IDispatchMessage message,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false); // delay-ok: InfiniteTimeSpan is block-until-cancelled -- there is no wall-clock duration here to be flaky

		return MessageResult.Success();
	}
}
