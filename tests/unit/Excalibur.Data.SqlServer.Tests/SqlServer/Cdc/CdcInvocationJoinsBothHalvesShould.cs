// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Threading.Channels;

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// A CDC invocation terminates both of its halves before it returns, and reports the failure that caused
/// the termination rather than the one caused by it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The invocation awaited its producer and then its consumer, in sequence. That is not a
/// join: it waits for a specific half FIRST, so the other is never observed when the first does not return
/// — and each half has a failure mode that leaves the other unable to return on its own.
/// </para>
/// <para>
/// A consumer fault leaves the producer blocked writing into a full bounded channel that no longer has a
/// reader, so awaiting the producer first never completes: the invocation hangs with no deadline, and the
/// caller's polling error-handling never runs because the call it would handle has not returned.
/// A producer fault propagates out of the first await and skips the second entirely, releasing the
/// invocation's execution lock while the consumer is still applying changes — so the next invocation begins
/// while work from the previous one is live, and that stale consumer completes under an identity the new
/// invocation has since replaced.
/// </para>
/// <para>
/// <b>WHY THESE ARMS DRIVE THE JOIN DIRECTLY.</b> Reproducing the hang through the public entry point needs
/// a live SQL Server, and a test that needs one cannot fail fast enough to be useful about a hang. The join
/// is the whole of the fix and its contract is fully stated in its own terms — terminate both, bound the
/// wait, surface the cause — so it is exercised with a real bounded <see cref="Channel{T}"/> whose writer
/// genuinely blocks. No wall clock is asserted on; the deadline exists only so a regression fails the run
/// instead of hanging it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcInvocationJoinsBothHalvesShould
{
	/// <summary>Generous enough that a slow machine never trips it, short enough that a hang fails the run.</summary>
	private static readonly TimeSpan JoinDeadline = TimeSpan.FromSeconds(10);

	private static readonly MethodInfo JoinMethod = typeof(CdcProcessor)
		.GetMethod("JoinBothHalvesAsync", BindingFlags.NonPublic | BindingFlags.Static)
		?? throw new InvalidOperationException("Expected the private static JoinBothHalvesAsync on CdcProcessor.");

	private static async Task InvokeJoinAsync(Task producer, Task consumer, CancellationTokenSource faultSource)
	{
		try
		{
			await ((Task)JoinMethod.Invoke(obj: null, [producer, consumer, faultSource])!).ConfigureAwait(false);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	/// <summary>
	/// Blocks on a real bounded channel that is already full, exactly as the producer does when the consumer
	/// has stopped reading. Only cancellation can release it.
	/// </summary>
	private static async Task BlockedWriterAsync(CancellationToken cancellationToken)
	{
		var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait });
		await channel.Writer.WriteAsync(1, cancellationToken).ConfigureAwait(false);

		// The channel is now full and nothing reads it, so this write blocks until the token is cancelled.
		await channel.Writer.WriteAsync(2, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// SAFETY + LIVENESS together. The producer is genuinely blocked on a full channel; the consumer faults.
	/// The join must RETURN (liveness — the pre-fix sequential await does not) and must report the
	/// CONSUMER's failure (safety — reporting the producer's cancellation would name the consequence and
	/// hide the cause).
	/// </summary>
	[Fact]
	public async Task ReturnAndReportTheConsumerFaultWhenTheProducerIsBlockedOnAFullChannel()
	{
		using var faultSource = new CancellationTokenSource();

		var producer = BlockedWriterAsync(faultSource.Token);
		var consumerFailure = new InvalidOperationException("the handler rejected a change");
		var consumer = Task.FromException(consumerFailure);

		var join = InvokeJoinAsync(producer, consumer, faultSource);

		var settled = await Task.WhenAny(join, Task.Delay(JoinDeadline)).ConfigureAwait(false);
		settled.ShouldBeSameAs(
			join,
			"the join did not return. A blocked producer cannot terminate on its own, so an invocation that "
			+ "waits for it without cancelling it hangs forever and the caller never gets the chance to "
			+ "handle the consumer failure");

		var thrown = await Should.ThrowAsync<Exception>(async () => await join).ConfigureAwait(false);
		thrown.ShouldBeSameAs(
			consumerFailure,
			"the consumer's failure is the CAUSE; the producer's cancellation is an effect of the join "
			+ "cancelling it. Reporting the effect would send an operator looking at the wrong half");
	}

	/// <summary>
	/// SAFETY. A producer fault must not let the invocation return while the consumer is still live — that
	/// is what released the execution lock early and let a later invocation replace the identity the stale
	/// consumer was still completing under.
	/// </summary>
	[Fact]
	public async Task NotReturnUntilTheConsumerHasAlsoTerminatedWhenTheProducerFaults()
	{
		using var faultSource = new CancellationTokenSource();

		var producerFailure = new InvalidOperationException("the change feed could not be read");
		var producer = Task.FromException(producerFailure);

		var consumerStillWorking = new TaskCompletionSource();
		using var registration = faultSource.Token.Register(() => consumerStillWorking.TrySetCanceled());
		var consumer = consumerStillWorking.Task;

		var join = InvokeJoinAsync(producer, consumer, faultSource);

		var settled = await Task.WhenAny(join, Task.Delay(JoinDeadline)).ConfigureAwait(false);
		settled.ShouldBeSameAs(join, "the join must terminate once it has cancelled the surviving half");

		var thrown = await Should.ThrowAsync<Exception>(async () => await join).ConfigureAwait(false);
		thrown.ShouldBeSameAs(producerFailure, "the producer's failure is the cause and must survive the join");

		consumer.IsCompleted.ShouldBeTrue(
			"the invocation returned while the consumer was still running. Its caller then releases the "
			+ "execution lock, so the next invocation starts alongside live work from this one");
	}

	/// <summary>
	/// LIVENESS. Nothing faults, so nothing may be cancelled. A join that cancelled unconditionally would
	/// satisfy both arms above and quietly truncate every successful batch.
	/// </summary>
	[Fact]
	public async Task LeaveBothHalvesAloneWhenNeitherFaults()
	{
		using var faultSource = new CancellationTokenSource();

		var producer = Task.CompletedTask;
		var consumer = Task.FromResult(7);

		await InvokeJoinAsync(producer, consumer, faultSource).ConfigureAwait(false);

		faultSource.IsCancellationRequested.ShouldBeFalse(
			"a successful batch must not be cancelled; cancelling here would abandon drained work");
		(await consumer.ConfigureAwait(false)).ShouldBe(7, "the consumer's result survives the join and is what the caller returns");
	}

	/// <summary>
	/// LIVENESS. A slow but successful pair must be waited for, not cut short — the join has no deadline of
	/// its own and must not acquire one.
	/// </summary>
	[Fact]
	public async Task WaitForASlowPairThatBothSucceed()
	{
		using var faultSource = new CancellationTokenSource();

		var producerFinish = new TaskCompletionSource();
		var consumerFinish = new TaskCompletionSource<int>();

		var join = InvokeJoinAsync(producerFinish.Task, consumerFinish.Task, faultSource);

		join.IsCompleted.ShouldBeFalse("neither half has finished, so the join cannot have");

		producerFinish.SetResult();
		consumerFinish.SetResult(3);

		var settled = await Task.WhenAny(join, Task.Delay(JoinDeadline)).ConfigureAwait(false);
		settled.ShouldBeSameAs(join);
		await join.ConfigureAwait(false);

		faultSource.IsCancellationRequested.ShouldBeFalse();
	}
}
