// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Concurrency;

/// <summary>
/// The double-checked initialisation pattern every store copies must run its body exactly once.
/// </summary>
/// <remarks>
/// <para>
/// Everything else written for this defect asks "does it throw?", and throwing is the SYMPTOM. The
/// property is that initialisation happens once. A store that keeps the semaphore but loses the
/// re-check inside the lock provisions twice — creates the indexes twice, the table twice — and
/// never throws at all. The structural test cannot see that either: a semaphore is present, so it
/// passes.
/// </para>
/// <para>
/// The second caller is admitted DETERMINISTICALLY rather than by winning a race. The first caller
/// is held inside the initialisation body while the second passes the outer check and queues on the
/// lock; only then is the first released. That ordering is forced, not hoped for, which is what
/// separates this from every other test written for this defect: the store's own concurrency test
/// detected the real bug in 2 runs out of 12, and the conformance barrier detected it in 0 of 5.
/// This fails every time the guard is wrong. It needs no container, no repeat count and no luck.
/// </para>
/// <para>
/// It binds the PATTERN rather than a store, deliberately. 43 types under src initialise lazily
/// and 36 of them hand-copy the same five lines (the other 7 are the tracked exceptions in
/// unguarded-lazy-init-baseline.txt), so one test covers the shape they share — and the RED case can be written down instead of
/// being reproduced by chance.
/// </para>
/// </remarks>
public sealed class LazyInitialisationRunsExactlyOnceShould
{
	[Fact]
	public async Task Run_The_Initialisation_Body_Once_When_A_Second_Caller_Arrives_During_It()
	{
		using var subject = new GuardedInitialiser();

		await DriveTwoCallersThroughTheWindowAsync(subject).ConfigureAwait(false);

		subject.TimesInitialised.ShouldBe(
			1,
			$"initialisation ran {subject.TimesInitialised} times. The second caller arrived while the "
			+ "first was still initialising, waited for the lock, and then repeated the work. Re-running "
			+ "initialisation is not merely wasteful: it re-executes index and schema creation, and any "
			+ "step in it that is not idempotent corrupts. The re-check INSIDE the lock is what prevents "
			+ "it — a caller that waited must ask again whether the work is already done.");
	}

	/// <summary>
	/// The RED case, written down rather than reproduced by luck: omitting the re-check inside the
	/// lock is the one-token mutation that breaks the pattern, and this proves the assertion above
	/// detects it — every run, not one in six.
	/// </summary>
	[Fact]
	public async Task Detect_A_Guard_That_Omits_The_Recheck_Inside_The_Lock()
	{
		using var broken = new MissingRecheckInitialiser();

		await DriveTwoCallersThroughTheWindowAsync(broken).ConfigureAwait(false);

		broken.TimesInitialised.ShouldBe(
			2,
			"the deliberately-broken subject did NOT repeat its initialisation, so the exactly-once "
			+ "assertion above has stopped being a detector and would now pass over a real regression. "
			+ "Either the interleaving is no longer being forced or the subject has drifted away from "
			+ "the shape the stores actually use.");
	}

	/// <summary>
	/// Forces the one interleaving that matters: caller B passes the outer check and queues on the
	/// lock while caller A is inside the body. No sleeps, no thread-pool luck — B's arrival is
	/// observed before A is allowed to finish.
	/// </summary>
	private static async Task DriveTwoCallersThroughTheWindowAsync(IInitialiser subject)
	{
		var first = subject.EnsureInitialisedAsync(CancellationToken.None);

		// A is inside the body and holding the lock. Until this completes there is no window to test.
		await subject.BodyEntered.ConfigureAwait(false);

		var second = subject.EnsureInitialisedAsync(CancellationToken.None);

		// B has read _initialized as false and is committed to the lock. Whether it has physically
		// reached WaitAsync yet does not change the outcome: once A releases, B acquires, and the
		// only question left is whether B re-checks. That is the property under test.
		//
		// Polled through the shared helper rather than a hand-rolled loop. The first version of this
		// was its own poller, which is how a fixed wall-clock wait gets reinvented one test at a
		// time: the shared one already bounds the wait, cancels cleanly and returns the moment the
		// condition holds. A short poll interval only because the condition becomes true almost
		// immediately here -- it is the responsiveness of the check, not a duration being waited out.
		var secondCallerArrived = await WaitHelpers.WaitUntilAsync(
			() => subject.CallersPastTheOuterCheck == 2,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(5)).ConfigureAwait(false);

		secondCallerArrived.ShouldBeTrue(
			"the second caller never passed the outer check, so the window under test was never "
			+ "entered and neither assertion means anything.");

		subject.ReleaseBody();
		await Task.WhenAll(first, second).ConfigureAwait(false);
	}

	private interface IInitialiser : IDisposable
	{
		/// <summary>Completes when a caller is inside the initialisation body holding the lock.</summary>
		Task BodyEntered { get; }

		/// <summary>Callers that have read the flag as false and are committed to taking the lock.</summary>
		int CallersPastTheOuterCheck { get; }

		int TimesInitialised { get; }

		Task EnsureInitialisedAsync(CancellationToken cancellationToken);

		/// <summary>Lets the caller inside the body finish.</summary>
		void ReleaseBody();
	}

	/// <summary>The shape every fixed store uses, reduced to the part under test.</summary>
	private sealed class GuardedInitialiser : IInitialiser
	{
		private readonly InitialisationProbe _probe = new();
		private readonly SemaphoreSlim _initLock = new(1, 1);
		private volatile bool _initialized;

		public Task BodyEntered => _probe.Entered;

		public int CallersPastTheOuterCheck => _probe.PastOuterCheck;

		public int TimesInitialised => _probe.Runs;

		public void Dispose() => _initLock.Dispose();

		public void ReleaseBody() => _probe.Release();

		public async Task EnsureInitialisedAsync(CancellationToken cancellationToken)
		{
			if (_initialized)
			{
				return;
			}

			_probe.RecordOuterCheckPassed();

			await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (_initialized)
				{
					return;
				}

				await _probe.RunBodyAsync().ConfigureAwait(false);
				_initialized = true;
			}
			finally
			{
				_ = _initLock.Release();
			}
		}
	}

	/// <summary>Identical, minus the re-check inside the lock.</summary>
	private sealed class MissingRecheckInitialiser : IInitialiser
	{
		private readonly InitialisationProbe _probe = new();
		private readonly SemaphoreSlim _initLock = new(1, 1);
		private volatile bool _initialized;

		public Task BodyEntered => _probe.Entered;

		public int CallersPastTheOuterCheck => _probe.PastOuterCheck;

		public int TimesInitialised => _probe.Runs;

		public void Dispose() => _initLock.Dispose();

		public void ReleaseBody() => _probe.Release();

		public async Task EnsureInitialisedAsync(CancellationToken cancellationToken)
		{
			if (_initialized)
			{
				return;
			}

			_probe.RecordOuterCheckPassed();

			await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				// The omission under test: no re-check here.
				await _probe.RunBodyAsync().ConfigureAwait(false);
				_initialized = true;
			}
			finally
			{
				_ = _initLock.Release();
			}
		}
	}

	/// <summary>
	/// Stands in for the real initialisation work — creating indexes, provisioning a table — and
	/// counts how many times it ran. It also lets the test hold the first caller inside the body,
	/// which is what makes the interleaving forced rather than raced.
	/// </summary>
	private sealed class InitialisationProbe
	{
		private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _pastOuterCheck;
		private int _runs;

		public Task Entered => _entered.Task;

		public int PastOuterCheck => Volatile.Read(ref _pastOuterCheck);

		public int Runs => Volatile.Read(ref _runs);

		public void RecordOuterCheckPassed() => Interlocked.Increment(ref _pastOuterCheck);

		public void Release() => _release.TrySetResult();

		public async Task RunBodyAsync()
		{
			_ = Interlocked.Increment(ref _runs);
			_ = _entered.TrySetResult();

			// An await inside the body, as the real ones have: they await index and schema creation.
			// Without one the whole method would complete synchronously and there would be no window.
			await _release.Task.ConfigureAwait(false);
		}
	}
}
