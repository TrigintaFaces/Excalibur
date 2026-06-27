// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Builders;
using Excalibur.Dispatch.Transport.Decorators;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Builders;

/// <summary>
/// Author≠impl regression lock for bd-no0lue (FR-D4): the internal <see cref="ReconnectingTransportSubscriber"/>
/// decorator was unreachable — there was no public opt-in seam to compose it into a subscriber pipeline. The fix
/// adds the <c>UseReconnect</c> builder extension (mirroring <c>UseDeadLetterQueue</c>) that wraps the inner
/// subscriber in a <see cref="ReconnectingTransportSubscriber"/> with the caller-supplied (clamped) backoff schedule.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity: pre-fix this lock does not compile — the <c>UseReconnect</c> extension did not exist, so the
/// "make the decorator reachable" guarantee had no entry point. The production RED-proof (the decorator was
/// previously unreachable via any public seam) is deferred to post-commit because the impl file
/// (<c>TransportSubscriberBuilderExtensions.cs</c>) is reserved by another lane; this lock binds the new seam.
/// </para>
/// <para>
/// Deterministic unit test — no real infrastructure, no wall-clock (the backoff schedule returns
/// <see cref="TimeSpan.Zero"/> so the reconnect loop never sleeps).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class UseReconnectShould
{
	[Fact]
	public void UseReconnect_AddsReconnectingDecorator()
	{
		var inner = A.Fake<ITransportSubscriber>();
		var builder = new TransportSubscriberBuilder(inner);

		var built = builder
			.UseReconnect(attempt => TimeSpan.FromMilliseconds(attempt), NullLoggerFactory.Instance)
			.Build();

		// FR-D4 reachability guarantee: the composed pipeline IS the reconnecting decorator wrapping the inner.
		built.ShouldBeOfType<ReconnectingTransportSubscriber>();
	}

	[Fact]
	public async Task UseReconnect_WiresBackoffScheduleIntoDecorator()
	{
		// Backoff observation: drive the wired decorator through one transient fault and assert it consults the
		// caller-supplied schedule (proving UseReconnect actually plumbs the Func<int,TimeSpan> into the decorator),
		// then re-subscribes. TimeSpan.Zero keeps the reconnect loop deterministic (no wall-clock dependency).
		var recordedAttempts = new List<int>();
		Func<int, TimeSpan> backoff = attempt =>
		{
			recordedAttempts.Add(attempt);
			return TimeSpan.Zero;
		};

		var inner = A.Fake<ITransportSubscriber>();
		A.CallTo(() => inner.Source).Returns("test-source");
		A.CallTo(() => inner.SubscribeAsync(
				A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
				A<CancellationToken>._))
			.Throws<InvalidOperationException>().Once()
			.Then.Returns(Task.CompletedTask);

		var built = new TransportSubscriberBuilder(inner)
			.UseReconnect(backoff, NullLoggerFactory.Instance)
			.Build();

		await built.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			CancellationToken.None);

		// The supplied schedule was consulted exactly once (for reconnect attempt #1) and the inner was
		// re-subscribed (faulted call + successful call = twice).
		recordedAttempts.ShouldBe([1]);
		A.CallTo(() => inner.SubscribeAsync(
			A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
			A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
	}
}
