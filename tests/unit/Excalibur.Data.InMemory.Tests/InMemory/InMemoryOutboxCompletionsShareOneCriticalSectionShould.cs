// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Every completion on this store runs in the SAME critical section, so a terminal-status guard cannot be
/// read in one member and acted on after another member has invalidated it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> Two completion overloads mutated message status, retry count, the lease map and the
/// next-attempt map with no lock, while their siblings on the same type mutated the same state under
/// <c>_claimLock</c>. A critical section excludes what shares its lock and <i>nothing else</i>, so a locked
/// member sitting beside an unlocked one that writes the same message is not serialised with it at all.
/// The guards read correctly and then wrote into a world that had changed underneath them.
/// </para>
/// <para>
/// <b>WHY IT MATTERS RATHER THAN BEING UNTIDY.</b> The failure path's terminal-status guard exists to stop
/// a late failure report dragging an already-delivered message back into the Failed status — which is
/// inside the claim predicate, so the message would be claimed and <i>delivered a second time</i>. Between
/// reading that guard and writing the status there was a window in which a concurrent mark-sent could
/// commit. The guard was never wrong; it was simply not in the same critical section as the write it
/// guarded, which is the only thing that makes a guard binding.
/// </para>
/// <para>
/// <b>WHAT THIS ARM CAN AND CANNOT PROVE.</b> It is a ONE-SIDED test and is stated as one. Correct code can
/// never fail it, so it is safe to run everywhere; incorrect code fails it only when the interleaving
/// lands, so a single green run of the pre-fix store would NOT have exonerated it. The iteration count is
/// chosen to make the pre-fix failure overwhelmingly likely rather than certain. A green here is evidence,
/// not proof — and that is the honest limit of any test of a race.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class InMemoryOutboxCompletionsShareOneCriticalSectionShould : IDisposable
{
	/// <summary>
	/// Enough contended attempts that an unsynchronised read-then-write is very likely to be caught, while
	/// staying fast enough to belong in the unit shard.
	/// </summary>
	private const int ContendedAttempts = 400;

	private readonly InMemoryOutboxStore _store;

	public InMemoryOutboxCompletionsShareOneCriticalSectionShould()
	{
		var options = Options.Create(new InMemoryOutboxOptions { MaxMessages = 100_000 });
		_store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
	}

	public void Dispose() => _store.Dispose();

	private async Task<string> StageAsync()
	{
		var message = new OutboundMessage("test.message", [1], "dest");
		await _store.StageMessageAsync(message, CancellationToken.None);
		return message.Id;
	}

	private async Task<HashSet<string>> FailedSetAsync()
	{
		var failed = await _store.GetAllTenantsFailedMessagesAsync(
			maxRetries: int.MaxValue, olderThan: null, batchSize: int.MaxValue, CancellationToken.None);

		return failed.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
	}

	/// <summary>
	/// SAFETY. A delivered message must never be observable as failed, whichever order the two completions
	/// are attempted in: mark-failed first then mark-sent leaves it Sent, and mark-sent first makes the
	/// terminal guard refuse the failure. Only a torn read-then-write produces a third outcome.
	/// </summary>
	[Fact]
	public async Task NeverLeaveADeliveredMessageInTheFailedSetWhenBothCompletionsRaceOnIt()
	{
		var ids = new List<string>(ContendedAttempts);

		for (var i = 0; i < ContendedAttempts; i++)
		{
			var id = await StageAsync();
			ids.Add(id);

			// Release both completions at the same instant so the contended window is actually entered,
			// rather than the first finishing before the second starts.
			using var gate = new Barrier(2);

			var markSent = Task.Run(() =>
			{
				gate.SignalAndWait();
				return _store.MarkSentAsync(id, CancellationToken.None).AsTask();
			});

			var markFailed = Task.Run(() =>
			{
				gate.SignalAndWait();
				return _store.MarkFailedAsync(id, "delivery rejected", retryCount: 1, CancellationToken.None).AsTask();
			});

			await Task.WhenAll(markSent, markFailed);
		}

		var failed = await FailedSetAsync();

		failed.Intersect(ids, StringComparer.Ordinal).ShouldBeEmpty(
			"a message that reached the terminal Sent status was afterwards recorded as Failed. Failed is "
			+ "inside the claim predicate, so the outbox will hand that message to a processor again and it "
			+ "will be delivered twice. The terminal guard on the failure path was read before the mark-sent "
			+ "committed and acted on afterwards, which is what happens when two members mutating one message "
			+ "do not share a critical section");
	}

	/// <summary>
	/// LIVENESS. The lock must not have been bought by making completions refuse each other: with no
	/// contention at all, an ordinary failure report still records the message as failed.
	/// </summary>
	[Fact]
	public async Task StillRecordAnUncontendedFailure()
	{
		var id = await StageAsync();

		await _store.MarkFailedAsync(id, "delivery rejected", retryCount: 1, CancellationToken.None);

		(await FailedSetAsync()).ShouldContain(id, "serialising the completions must not stop them completing");
	}

	/// <summary>
	/// LIVENESS. The other half of the same concern: an ordinary mark-sent still removes the message from
	/// the failed set rather than deadlocking or silently refusing.
	/// </summary>
	[Fact]
	public async Task StillDeliverAnUncontendedMessageThatPreviouslyFailed()
	{
		var id = await StageAsync();

		await _store.MarkFailedAsync(id, "delivery rejected", retryCount: 1, CancellationToken.None);
		(await FailedSetAsync()).ShouldContain(id);

		await _store.MarkSentAsync(id, CancellationToken.None);

		(await FailedSetAsync()).ShouldNotContain(id, "a delivered message leaves the failed set");
	}

	/// <summary>
	/// LIVENESS. Many uncontended completions across distinct messages must all apply — an implementation
	/// that serialised itself into dropping work would pass the safety arm above and fail here.
	/// </summary>
	[Fact]
	public async Task ApplyEveryCompletionWhenManyMessagesCompleteConcurrently()
	{
		var ids = new List<string>();
		for (var i = 0; i < 200; i++)
		{
			ids.Add(await StageAsync());
		}

		await Task.WhenAll(ids.Select(id =>
			Task.Run(() => _store.MarkFailedAsync(id, "delivery rejected", retryCount: 1, CancellationToken.None).AsTask())));

		var failed = await FailedSetAsync();
		ids.ShouldAllBe(id => failed.Contains(id), "every failure reported must be recorded, not merely serialised");
	}
}
