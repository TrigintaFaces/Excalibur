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
}
