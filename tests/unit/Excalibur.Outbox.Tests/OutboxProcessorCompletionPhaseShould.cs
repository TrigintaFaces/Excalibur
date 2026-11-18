// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Resilience;
using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Tests;

public sealed partial class OutboxProcessorTenureShould
{
	private static readonly string[] PhaseExpectedDeliveredIds = ["A"];
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task NeverReportAcceptedDeliveryAsCircuitShortCircuitWhenCompletionThrows(int degree)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A");
		var fault = new CircuitBreakerOpenException("completion-store");
		fixture.Mark = (_, _) => ValueTask.FromException<OutboxCompletionOutcome>(fault);
		var reports = new ConcurrentQueue<(int Attempts, DateTimeOffset? Schedule)>();
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily((string _, string _, int attempts, DateTimeOffset? schedule, OutboxWriteAuthority _, CancellationToken _) =>
			{
				reports.Enqueue((attempts, schedule));
				return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
			});
		var logger = new CompletionPhaseLogger();
		fixture.Logger = logger;
		await using var processor = fixture.Create(degree: degree, minimized: true);
		var accepted = await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		accepted.ShouldBe(1);
		fixture.Delivered.ToArray().ShouldBe(PhaseExpectedDeliveredIds);
		fixture.Marked.ToArray().ShouldBe(new[] { ("A", 5L) });
		reports.ShouldBeEmpty("a postacceptance bookkeeping fault is not a delivery short-circuit or retry");
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>._, A<CancellationToken>._)).MustNotHaveHappened();
		fixture.AssertNoUnfencedCompletion();
		logger.Errors.ShouldContain(error => ReferenceEquals(error, fault));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public async Task PreserveGenuinePreAcceptanceCircuitShortCircuitWithoutConsumingAttempt(int degree)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A");
		fixture.Dispatch = (_, _) => Task.FromException<IMessageResult>(new CircuitBreakerOpenException("transport"));
		var reports = new ConcurrentQueue<(int Attempts, DateTimeOffset? Schedule)>();
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			"A", A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily((string _, string _, int attempts, DateTimeOffset? schedule, OutboxWriteAuthority authority, CancellationToken _) =>
			{
				authority.ShouldBe(new OutboxWriteAuthority(5, "claim-A"));
				reports.Enqueue((attempts, schedule));
				return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
			});
		await using var processor = fixture.Create(degree: degree, minimized: true);
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		fixture.Delivered.ToArray().ShouldBe(PhaseExpectedDeliveredIds);
		fixture.Marked.ShouldBeEmpty();
		reports.ToArray().ShouldBe(new (int, DateTimeOffset?)[] { (0, null) });
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>._, A<CancellationToken>._)).MustNotHaveHappened();
		fixture.AssertNoUnfencedCompletion();
	}

	private sealed class CompletionPhaseLogger : ILogger<OutboxProcessor>
	{
		internal ConcurrentQueue<Exception> Errors { get; } = new();
		public bool IsEnabled(LogLevel level) => true;
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (exception is not null) { Errors.Enqueue(exception); }
		}
	}
}
