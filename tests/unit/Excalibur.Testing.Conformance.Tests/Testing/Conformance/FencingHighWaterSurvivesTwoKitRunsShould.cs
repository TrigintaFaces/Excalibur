// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

namespace Excalibur.Testing.Conformance.Tests.Testing.Conformance;

/// <summary>
/// The fencing state of a persistent store, held OUTSIDE the store object exactly as a database holds it.
/// </summary>
/// <remarks>
/// This is the whole point of the fixture. An in-memory store keeps its high-water mark in the instance, so
/// a fresh store per arm has a fresh mark and cross-run poisoning is structurally unobservable. A real
/// database does not: the mark outlives every store object that ever connected to it. Two kit runs are
/// therefore modelled as two store objects sharing one state.
/// </remarks>
internal sealed class SharedOutboxState
{
	public long? HighWater { get; set; }

	/// <summary>
	/// The highest mark ever recorded, which the restore does NOT lower.
	/// </summary>
	/// <remarks>
	/// This is what a consumer is left holding if a certification run aborts -- a crash, a cancelled CI
	/// job, a failed arm that takes the process down -- before anything restores the mark. The restore
	/// closes the tidy path; the peak is what the untidy one leaves.
	/// </remarks>
	public long Peak { get; set; }

	public List<OutboundMessage> Rows { get; } = [];
}

/// <summary>An outbox store whose fencing mark is durable across store instances.</summary>
internal sealed class PersistentFencedOutboxStore(SharedOutboxState state)
	: IOutboxStore, IFencedOutboxStore, IFencedOutboxStoreDiagnostics
{
	public object? GetService(Type serviceType) =>
		serviceType == typeof(IFencedOutboxStore) || serviceType == typeof(IFencedOutboxStoreDiagnostics)
			? this
			: null;

	public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		state.Rows.Add(message);
		return ValueTask.CompletedTask;
	}

	public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
		=> throw new NotSupportedException();

	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		=> ValueTask.FromResult(Unsent(batchSize));

	public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		long fencingToken,
		CancellationToken cancellationToken)
	{
		// A stale CLAIM is handed no work rather than throwing -- a superseded leader must simply find
		// nothing to do. Only a stale MUTATION throws. The two halves are different contracts and the kit
		// asserts each separately.
		if (state.HighWater is { } current && fencingToken < current)
		{
			return ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);
		}

		Adopt(fencingToken);
		return ValueTask.FromResult(Unsent(batchSize));
	}

	public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		Send(messageId);
		return ValueTask.CompletedTask;
	}

	public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken)
	{
		Fence(fencingToken);
		Send(messageId);
		return ValueTask.CompletedTask;
	}

	public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		=> ValueTask.CompletedTask;

	public Task<long?> GetFencingHighWaterAsync(CancellationToken cancellationToken)
		=> Task.FromResult(state.HighWater);

	public Task ResetFencingHighWaterAsync(long newHighWater, bool force, CancellationToken cancellationToken)
	{
		if (!force && state.HighWater is { } current && newHighWater < current)
		{
			throw new InvalidOperationException("refusing to lower the high-water without force");
		}

		state.HighWater = newHighWater;
		return Task.CompletedTask;
	}

	/// <summary>Raises the mark to the presented token. Monotonic by design: it never falls.</summary>
	private void Adopt(long fencingToken)
	{
		state.HighWater = state.HighWater is { } h ? Math.Max(h, fencingToken) : fencingToken;
		state.Peak = Math.Max(state.Peak, state.HighWater.Value);
	}

	/// <summary>The mutation fence: refuse a token below the mark, otherwise adopt it.</summary>
	private void Fence(long fencingToken)
	{
		if (state.HighWater is { } current && fencingToken < current)
		{
			// Report BOTH tokens: the kit requires a refusal to say what was presented and what it lost to,
			// and this fixture is meant to be a conforming store whose only special property is a durable
			// mark -- so a failing arm indicts the kit's token handling and nothing else.
			throw new StaleOutboxFencingTokenException(
				$"token {fencingToken} is stale against high-water {current}")
			{
				PresentedToken = fencingToken,
				HighWaterToken = current,
			};
		}

		Adopt(fencingToken);
	}

	private IEnumerable<OutboundMessage> Unsent(int batchSize) =>
		state.Rows.Where(static r => r.SentAt is null).Take(batchSize).ToList();

	private void Send(string messageId)
	{
		var row = state.Rows.Find(r => string.Equals(r.Id, messageId, StringComparison.Ordinal));

		if (row is not null)
		{
			row.SentAt = DateTimeOffset.UtcNow;
		}
	}
}

/// <summary>One conformance run against a given store object.</summary>
internal sealed class OneKitRun(IOutboxStore store) : OutboxStoreConformanceTestKit
{
	protected override bool ParticipatesInFencing => true;

	protected override Task<IOutboxStore> CreateStoreAsync() => Task.FromResult(store);

	protected override Task ResetDataAsync() => Task.CompletedTask;

	/// <summary>Runs the fencing arms a certification run would run.</summary>
	public async Task RunFencingArmsAsync()
	{
		await Fencing_CurrentLeaderToken_ShouldClaimAndComplete().ConfigureAwait(false);
		await Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation().ConfigureAwait(false);
	}
}

/// <summary>
/// The cross-run property: after two consecutive conformance runs against ONE persistent store, an
/// election whose tokens are a counter starting at one must still be accepted.
/// </summary>
/// <remarks>
/// A run-once arm cannot detect this class. The poisoning is only observable across runs, because a single
/// run's arms each reason relative to whatever mark they find and are satisfied by any of them.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FencingHighWaterSurvivesTwoKitRunsShould
{
	[Fact]
	public async Task LeaveAMarkThatACounterBasedElectionCanStillExceed()
	{
		var state = new SharedOutboxState();

		// Two runs, two store objects, ONE durable state -- a consumer certifying twice against their own
		// database.
		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);
		var afterFirstRun = state.HighWater;

		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);
		var afterSecondRun = state.HighWater;

		// THE PROPERTY. Every shipped token provider mints a counter from one (a sequence, an INCR). If the
		// kit left a mark at or above that, the consumer's next real leader is refused -- and because the
		// mark only ever rises, that scope never recovers on its own.
		var store = new PersistentFencedOutboxStore(state);
		var message = new OutboundMessage { Id = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow };
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var refusal = await Record.ExceptionAsync(
			async () => await ((IFencedOutboxStore)store)
				.MarkSentAsync(message.Id, 1L, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		refusal.ShouldBeNull(
			$"after two conformance runs the store's fencing high-water is {afterSecondRun} (it was "
			+ $"{afterFirstRun} after one run), so a real election minting token 1 is refused and that scope "
			+ "stalls permanently. A conformance kit must not leave a consumer's store in a state the "
			+ "consumer's own production path cannot reach.");
	}

	/// <summary>
	/// The mark a run REACHES must stay within reach of a counter, not only the mark it leaves.
	/// </summary>
	/// <remarks>
	/// The restore closes the tidy path, and on its own it makes the token's magnitude invisible: a mark of
	/// 1.79e12 and a mark of 10,000 are both returned to the floor, so a test that only inspects the final
	/// value cannot tell a derived token from a clock-anchored one. What separates them is an ABORTED run.
	/// If the process dies between an arm advancing the mark and anything restoring it, the consumer keeps
	/// whatever the run reached -- and a wall-clock token leaves a value no counter-based election will
	/// ever mint, which is unrecoverable without an administrative reset.
	/// </remarks>
	[Fact]
	public async Task KeepTheMarkItREACHESWithinReachOfACounter_NotOnlyTheMarkItLeaves()
	{
		var state = new SharedOutboxState();

		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);

		// A generous ceiling: a real election mints a counter, so anything a busy scope reaches in normal
		// operation is small. Epoch milliseconds (about 1.79e12) is not in that range and never will be.
		const long ReachableByACounter = 1_000_000L;

		state.Peak.ShouldBeLessThan(
			ReachableByACounter,
			$"a single conformance run drove the fencing mark to {state.Peak}. The restore returns it, but a "
			+ "run that aborts first leaves the consumer holding this value, and a counter-based election "
			+ "cannot reach it. Derive the token from the mark the store already carries instead of the "
			+ "wall clock.");
	}

	/// <summary>
	/// The kit must work against a store ALREADY IN USE, whose mark is well past zero.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that distinguishes "derive the token from the mark the store carries" from "assume
	/// the mark is zero". Against a virgin store the two are indistinguishable, because zero is the right
	/// answer by accident -- so a fixture that starts at zero cannot detect a kit that never reads the
	/// store at all.
	/// </para>
	/// <para>
	/// A consumer's database is not virgin. Its elections have already run, so its mark is whatever they
	/// reached. A kit minting from zero presents tokens BELOW that mark, and every one of them is correctly
	/// refused -- the arms then fail and blame the consumer's store for refusing tokens the kit should never
	/// have minted.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RunAgainstAStoreWhoseElectionsHaveAlreadyAdvancedTheMark()
	{
		// A scope whose real elections have run fifty thousand times. Nothing unusual about that.
		var state = new SharedOutboxState { HighWater = 50_000L, Peak = 50_000L };

		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);

		state.HighWater.ShouldBe(
			50_000L,
			"the kit did not return the mark to the value the store already carried, so a consumer "
			+ "certifying against a database in use has had its fencing state moved");
	}

	[Fact]
	public async Task NotAccumulateAcrossRuns_SoASecondRunIsNoWorseThanTheFirst()
	{
		var state = new SharedOutboxState();

		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);
		var afterFirstRun = state.HighWater;

		await new OneKitRun(new PersistentFencedOutboxStore(state)).RunFencingArmsAsync().ConfigureAwait(false);
		var afterSecondRun = state.HighWater;

		// Monotonic drift is the failure shape: if each run raises the mark, enough runs poison any store
		// whatever the per-run increment is.
		afterSecondRun.ShouldBe(
			afterFirstRun,
			"the mark rose between two identical runs, so it accumulates and enough certification runs will "
			+ "put it beyond any counter-based election regardless of how small each run's advance is");
	}
}
