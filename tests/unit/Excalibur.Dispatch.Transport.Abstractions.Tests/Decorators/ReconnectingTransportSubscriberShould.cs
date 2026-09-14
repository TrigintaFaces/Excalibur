// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

/// <summary>
/// Author≠impl regression lock for S853 · <c>kxexrz</c> — the shared <see cref="ReconnectingTransportSubscriber"/>
/// that gives every transport a uniform self-healing receive/stream loop.
/// </summary>
/// <remarks>
/// <para>
/// Contract (AC-B3): when the inner subscription faults with a <b>non-cancellation</b> error, the decorator
/// backs off (per the injected <c>Func&lt;int, TimeSpan&gt;</c> schedule) and <b>re-subscribes</b> so
/// consumption continues; a <b>cooperative cancellation</b> (<see cref="OperationCanceledException"/> while the
/// token is cancelled) <b>propagates</b> and is never retried. A normal return ends the subscription without a
/// reconnect.
/// </para>
/// <para>
/// The backoff is a plain delegate (no resilience-library dependency) — mirroring how
/// <c>DeadLetterTransportSubscriber</c> takes a handler delegate to keep <c>Transport.Abstractions</c>
/// lightweight (SA 16385). The backoff is injected so tests are deterministic (zero real delay).
/// </para>
/// <para>
/// <b>Non-vacuity (RED mutants):</b> removing the reconnect loop (rethrow the fault) makes
/// <c>ReSubscribes_OnTransientFault</c> RED (inner called once, fault escapes); removing the OCE filter
/// (treating OCE as a reconnectable fault) makes <c>Propagates_OnCancellation_NoRetry</c> RED (it would loop
/// instead of propagating). Drafted by the implementer (PlatformDeveloper) under PM 16453; independently
/// reviewed + RED-proven + augmented by TestsDeveloper.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ReconnectingTransportSubscriberShould
{
	private static readonly Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> Handler =
		(_, _) => Task.FromResult(MessageAction.Acknowledge);

	[Fact]
	public void Throw_On_Null_BackoffDelay()
	{
		var inner = A.Fake<ITransportSubscriber>();

		Should.Throw<ArgumentNullException>(() =>
			new ReconnectingTransportSubscriber(inner, null!, NullLogger<ReconnectingTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_On_Null_Logger()
	{
		var inner = A.Fake<ITransportSubscriber>();

		Should.Throw<ArgumentNullException>(() =>
			new ReconnectingTransportSubscriber(inner, _ => TimeSpan.Zero, null!));
	}

	[Fact]
	public async Task ReSubscribe_OnTransientFault_ThenContinue()
	{
		// Inner faults (non-OCE) on the first subscribe, then returns normally on the second.
		var attempts = new List<int>();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");

		var calls = 0;
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => ++calls == 1
				? throw new InvalidOperationException("transient receive fault")
				: Task.CompletedTask);

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await subscriber.SubscribeAsync(Handler, CancellationToken.None);

		// Re-subscribed exactly once: inner invoked twice, backoff invoked once with attempt #1.
		calls.ShouldBe(2);
		attempts.ShouldBe([1]);
	}

	[Fact]
	public async Task ReSubscribe_MultipleTransientFaults_WithIncreasingAttempt()
	{
		// Two consecutive faults, succeeding on the third subscribe — backoff is consulted per attempt.
		var attempts = new List<int>();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");

		var calls = 0;
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(() => ++calls <= 2
				? throw new InvalidOperationException("transient receive fault")
				: Task.CompletedTask);

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await subscriber.SubscribeAsync(Handler, CancellationToken.None);

		calls.ShouldBe(3);
		attempts.ShouldBe([1, 2]);
	}

	[Fact]
	public async Task Propagate_OnCancellation_NoRetry()
	{
		// The inner subscription is cancelled mid-flight: it throws OCE while the token is cancelled.
		// The decorator must propagate it and NOT reconnect (no backoff consulted).
		var attempts = new List<int>();
		using var cts = new CancellationTokenSource();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");

		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				cts.Cancel();
				throw new OperationCanceledException(cts.Token);
			});

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		attempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task Propagate_OnPreCancelledToken_WithoutSubscribing()
	{
		// An already-cancelled token short-circuits before the first subscribe — OCE propagates,
		// the inner subscriber is never invoked and no backoff is consulted.
		var attempts = new List<int>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
		attempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task NotReconnect_WhenInnerReturnsNormally()
	{
		// A normal return (e.g. the subscription completed) ends the loop — no reconnect, no backoff.
		var attempts = new List<int>();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await subscriber.SubscribeAsync(Handler, CancellationToken.None);

		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		attempts.ShouldBeEmpty();
	}

	[Fact]
	public async Task ConsultTheBackoff_OnEveryAttempt_AgainstAPermanentlyFaultingInner()
	{
		// A permanently-faulting inner is the case the loop is uncapped for: a subscriber that gave up
		// would stop consuming silently and forever, so the schedule -- not an attempt limit -- is what
		// governs the pace. That only holds if the schedule is consulted on EVERY attempt, numbered in
		// order, with none skipped.
		//
		// The INNER ends the run, not the backoff. A decorator that skipped or mis-numbered attempts must
		// fail this arm, and it cannot do that if the thing under test also controls when the test stops.
		const int SubscribeAttempts = 5;

		var attempts = new List<int>();
		using var cts = new CancellationTokenSource();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");

		var calls = 0;
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily<Task>(() =>
			{
				if (++calls >= SubscribeAttempts)
				{
					cts.Cancel();
				}

				throw new InvalidOperationException("the broker is down and is staying down");
			});

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			attempt => { attempts.Add(attempt); return TimeSpan.Zero; },
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		calls.ShouldBe(SubscribeAttempts);

		// Every fault consulted the schedule, in order, with no gaps: 1..5 and nothing else.
		attempts.ShouldBe([.. Enumerable.Range(1, SubscribeAttempts)]);
	}

	[Fact]
	public async Task LetTheScheduleGovernThePace_NotTheLoop()
	{
		// Runaway detection. A yield-based wait cannot tell a paced loop from one that ignores the delay it
		// was given -- only real elapsed time can -- so this arm measures the clock.
		//
		// The run ends on the attempt COUNT, not on a timer: a loop that ignores the delay would starve the
		// thread pool and a timer-based stop would hang instead of failing. Counting terminates either way,
		// in milliseconds when the defect is present.
		var floor = TimeSpan.FromMilliseconds(20);
		const int AttemptsToObserve = 10;

		// Half the ideal 200ms, so ordinary scheduling jitter and clock granularity cannot fail it, while a
		// loop that skips the wait finishes in single-digit milliseconds and cannot pass.
		var minimumHonestElapsed = TimeSpan.FromMilliseconds(100);

		var attempts = 0;
		using var cts = new CancellationTokenSource();
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily<Task>(() => throw new InvalidOperationException("the broker is down"));

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			_ =>
			{
				if (++attempts >= AttemptsToObserve)
				{
					cts.Cancel();
				}

				return floor;
			},
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		var started = System.Diagnostics.Stopwatch.StartNew();
		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));
		started.Stop();

		// Liveness: it kept reconnecting. A decorator that gave up after one fault would leave the
		// subscriber permanently dead, and would satisfy any elapsed-time bound by never running.
		attempts.ShouldBe(AttemptsToObserve);

		// Safety: the schedule governed the pace. Waiting for the delay it returned is the only way those
		// attempts can have taken this long.
		started.Elapsed.ShouldBeGreaterThan(minimumHonestElapsed,
			$"{AttemptsToObserve} reconnect attempts on a {floor.TotalMilliseconds}ms schedule took only "
			+ $"{started.ElapsedMilliseconds}ms -- the loop is ignoring the delay it was given");
	}

	[Fact]
	public async Task KeepReconnecting_WhenTheScheduleReturnsANegativeDelay()
	{
		// SAFETY. The reconnect loop is uncapped on purpose, so the schedule is the only thing governing its
		// pace -- and nothing used to constrain what the schedule could return. A negative delay is the
		// sharpest case: Task.Delay rejects it, and that rejection is raised OUTSIDE the catch that handles
		// receive faults, so the whole subscription ends on an ArgumentOutOfRangeException. The subscriber
		// stops consuming and never resumes, which is precisely the outcome the uncapped loop exists to
		// prevent, reached through the one input the caller fully controls.
		//
		// RED against a decorator with no floor: the first fault ends the run with ArgumentOutOfRangeException
		// after exactly one attempt.
		const int AttemptsToObserve = 5;

		var attempts = 0;
		using var cts = new CancellationTokenSource();
		var inner = PermanentlyFaultingInner();

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			_ =>
			{
				if (++attempts >= AttemptsToObserve)
				{
					cts.Cancel();
				}

				return TimeSpan.FromSeconds(-5);
			},
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		attempts.ShouldBe(AttemptsToObserve,
			"a negative delay from the schedule must be raised to the floor and the loop must keep "
			+ "reconnecting. Passing it to Task.Delay throws past the fault handler and kills the "
			+ "subscription, which stops consumption permanently with no further error.");
	}

	[Fact]
	public async Task KeepReconnecting_WhenTheScheduleReturnsAnInfiniteDelay()
	{
		// SAFETY, and the quietest of the three. TimeSpan.FromMilliseconds(-1) is Timeout.InfiniteTimeSpan:
		// Task.Delay accepts it and waits forever. Without a floor the subscriber does not crash, does not
		// log again and does not consume -- it simply stops after the first fault and stays stopped, which is
		// indistinguishable from a healthy idle subscriber from the outside.
		//
		// The run is ended by the attempt count, with a wall-clock cancel only as a backstop so the
		// no-floor case fails rather than hangs.
		const int AttemptsToObserve = 5;

		var attempts = 0;
		using var cts = new CancellationTokenSource();
		cts.CancelAfter(TimeSpan.FromSeconds(10));
		var inner = PermanentlyFaultingInner();

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			_ =>
			{
				if (++attempts >= AttemptsToObserve)
				{
					cts.Cancel();
				}

				return Timeout.InfiniteTimeSpan;
			},
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		attempts.ShouldBe(AttemptsToObserve,
			"an infinite delay from the schedule must be raised to the floor. Honouring it parks the "
			+ "reconnect loop forever after a single fault, so the subscriber consumes nothing and reports "
			+ "nothing.");
	}

	[Fact]
	public async Task KeepMakingProgress_WhenTheScheduleReturnsZero()
	{
		// LIVENESS, and the arm that bounds the floor from ABOVE. The two arms before this one prove the
		// floor fires; nothing in them prevents it from being raised to a value that overrides a schedule a
		// consumer actually chose, and a floor is only defensible while it stays too small to do that.
		//
		// It asserts progress rather than elapsed time, deliberately. The floor is below the platform timer's
		// resolution, so no wall-clock assertion could tell a floored zero from a real one -- but a floor
		// large enough to matter starves the attempt count against the backstop, which is observable and
		// terminates. Verified: with the floor at one second this arm is the one that goes red.
		const int AttemptsToObserve = 20;

		var attempts = 0;
		using var cts = new CancellationTokenSource();
		cts.CancelAfter(TimeSpan.FromSeconds(10));
		var inner = PermanentlyFaultingInner();

		var subscriber = new ReconnectingTransportSubscriber(
			inner,
			_ =>
			{
				if (++attempts >= AttemptsToObserve)
				{
					cts.Cancel();
				}

				return TimeSpan.Zero;
			},
			NullLogger<ReconnectingTransportSubscriber>.Instance);

		await Should.ThrowAsync<OperationCanceledException>(
			() => subscriber.SubscribeAsync(Handler, cts.Token));

		attempts.ShouldBe(AttemptsToObserve,
			"a zero schedule must still reconnect -- the floor paces the loop, it does not stop it.");
	}

	private static ITransportSubscriber PermanentlyFaultingInner()
	{
		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-topic");
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.ReturnsLazily<Task>(() => throw new InvalidOperationException("the broker is down and is staying down"));

		return inner;
	}
}

