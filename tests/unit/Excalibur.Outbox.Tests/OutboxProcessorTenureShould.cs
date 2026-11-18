// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.Diagnostics;

using DeliveryOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed partial class OutboxProcessorTenureShould
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopSequentialTransportAdmissionsAfterReportedOrThrownRefusal(bool thrown)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A", "B");
		fixture.Mark = (_, _) => Refusal(thrown);
		await using var processor = fixture.Create();

		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);

		fixture.Delivered.ToArray().ShouldBe(["A"]);
		fixture.Marked.ToArray().ShouldBe([("A", 5L)]);
		fixture.AssertNoUnfencedCompletion();
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(OutboxCompletionOutcome.MessageNotFound)]
	public async Task ContinueTheSameTenureAfterARowScopedRefusal(OutboxCompletionOutcome outcome)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A", "B");
		fixture.Mark = (id, _) => ValueTask.FromResult(id == "A" ? outcome : OutboxCompletionOutcome.Applied);
		await using var processor = fixture.Create();

		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(2);
		fixture.Delivered.ToArray().ShouldBe(["A", "B"]);
		fixture.Marked.ToArray().ShouldBe([("A", 5L), ("B", 5L)]);
	}

	[Fact]
	public async Task RememberRefusalAcrossRecreatedProcessorsUntilANewerAuthorizedTenure()
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A");
		fixture.Mark = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await using (var first = fixture.Create())
		{
			(await first.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		var claims = fixture.Claims;
		fixture.Enqueue("B");
		fixture.Mark = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		await using (var second = fixture.Create())
		{
			(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
			fixture.Gate.SetToken(null);
			(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
			fixture.Gate.SetToken(5);
			(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		}
		fixture.Claims.ShouldBe(claims, "a new processor or an intervening null token cannot erase a learned refusal");
		fixture.Delivered.ToArray().ShouldBe(["A"]);
		fixture.Gate.SetToken(6);
		await using (var newer = fixture.Create())
		{
			(await newer.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		fixture.Delivered.ToArray().ShouldBe(["A", "B"]);
		fixture.Marked.ShouldContain(("B", 6L));
	}

	[Fact]
	public async Task KeepEqualNumericTokensInDifferentAuthorityDomainsIndependent()
	{
		await using var refused = new Harness();
		await using var unrelated = new Harness();
		refused.Gate.Equals(unrelated.Gate).ShouldBeTrue("hostile equality is deliberate; domain identity must be by reference");
		refused.Enqueue("refused");
		refused.Mark = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await using var first = refused.Create();
		(await first.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		unrelated.Enqueue("independent");
		await using var second = unrelated.Create();
		(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		unrelated.Marked.ToArray().ShouldBe([("independent", 5L)]);
	}

	[Fact]
	public async Task ShareRefusalAcrossTransientWrappersOnlyWhenTheirExplicitAuthorityDomainMatches()
	{
		var domain = new object();
		await using var refused = new Harness(new DomainGate(domain));
		await using var recreated = new Harness(new DomainGate(domain));
		await using var unrelated = new Harness(new DomainGate(new object()));
		refused.Enqueue("A");
		refused.Mark = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await using (var first = refused.Create())
		{
			(await first.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		recreated.Enqueue("B");
		await using (var second = recreated.Create())
		{
			(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		}
		recreated.Claims.ShouldBe(0);
		recreated.Delivered.ShouldBeEmpty();
		unrelated.Enqueue("independent");
		await using (var other = unrelated.Create())
		{
			(await other.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		recreated.Gate.SetToken(6);
		await using (var newer = recreated.Create())
		{
			(await newer.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		recreated.Marked.ToArray().ShouldBe([("B", 6L)]);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverRelabelAnOldClaimWithTheNewLiveToken(bool failure)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A", "B");
		fixture.Dispatch = (_, _) =>
		{
			fixture.Gate.SetToken(6);
			return Task.FromResult<IMessageResult>(failure ? MessageResult.Failed("controlled transport failure") : MessageResult.Success());
		};
		await using var processor = fixture.Create();
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(failure ? 0 : 1);
		fixture.Delivered.ToArray().ShouldBe(["A"]);
		fixture.Marked.ShouldNotContain(item => item.Token == 6);
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._,
			A<OutboxWriteAuthority>.That.Matches(authority => authority.FencingToken == 6), A<CancellationToken>._)).MustNotHaveHappened();
		fixture.AssertNoUnfencedCompletion();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopAfterAFencedFailureReportRefusesTheTenure(bool thrown)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A", "B");
		fixture.Dispatch = (_, _) => Task.FromResult<IMessageResult>(MessageResult.Failed("controlled failure"));
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			A<string>._, A<string>._, A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily(() => Refusal(thrown));
		await using var processor = fixture.Create();
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		fixture.Delivered.ToArray().ShouldBe(["A"]);
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)fixture.Store).MarkFailedAsync(
			"A", A<string>._, 1, A<DateTimeOffset?>._,
			A<OutboxWriteAuthority>.That.Matches(authority => authority.FencingToken == 5 && authority.ClaimIdentity == "claim-A"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		fixture.AssertNoUnfencedCompletion();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task JoinAnAlreadyAdmittedParallelDispatchButExcludeQueuedWorkAfterRefusal(bool thrown)
	{
		await using var fixture = new Harness();
		var warmupEntered = Signal();
		var allQueued = Signal();
		var siblingEntered = Signal();
		var refusalLearned = Signal();
		var releaseSibling = Signal();
		var refusalEventId = thrown ? OutboxEventId.OutboxFencedMarkSentRefused : OutboxEventId.OutboxFencedMarkSentRefusedOutcome;
		var logger = new RefusalMilestoneLogger(refusalEventId, refusalLearned);
		fixture.Logger = logger;
		var claim = 0;
		fixture.Claim = async (_, _) =>
		{
			switch (Interlocked.Increment(ref claim))
			{
				case 1: return [CreateMessage("warmup")];
				case 2:
					await warmupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
					return [CreateMessage("A"), CreateMessage("B"), CreateMessage("C")];
				default:
					allQueued.TrySetResult();
					return [];
			}
		};
		fixture.Dispatch = async (id, _) =>
		{
			if (id == "warmup") { warmupEntered.TrySetResult(); await allQueued.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
			if (id == "A") { await siblingEntered.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
			if (id == "B") { siblingEntered.TrySetResult(); await releaseSibling.Task; }
			return MessageResult.Success();
		};
		fixture.Mark = (id, _) =>
		{
			if (id != "A") { return ValueTask.FromResult(OutboxCompletionOutcome.Applied); }
			return Refusal(thrown);
		};
		using var metrics = new BatchFailureObserver();
		await using var processor = metrics.Capture(() => fixture.Create(degree: 2, consumerBatch: 3, minimized: true));
		var run = processor.DispatchPendingMessagesAsync(CancellationToken.None);
		try
		{
			await refusalLearned.Task.WaitAsync(TimeSpan.FromSeconds(10));
			run.IsCompleted.ShouldBeFalse("the actual already-admitted sibling still owns a transport operation");
			fixture.Delivered.ShouldContain("A");
			fixture.Delivered.ShouldContain("B");
		}
		finally
		{
			releaseSibling.TrySetResult();
			_ = await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
		}
		(await run.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(3);
		fixture.Delivered.ShouldNotContain("C");
		fixture.Marked.ShouldNotContain(item => item.Id == "C");
		fixture.AssertNoUnfencedCompletion();
		logger.ObservedRefusals.ShouldBe(1, "the owned logger must observe the actual post-refusal branch before the held sibling is released");
		metrics.CompletedBatches.ShouldBeGreaterThan(0, "the successful warmup is a positive metric-listener control");
		metrics.Failures.ShouldBe(0, "queued work refused by the tenure must not be counted as a transport failure");
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task StopLaterConsumerBatchesAndFurtherCompletionsAfterAtLeastOnceRefusal(bool batchDatabase, bool thrown)
	{
		await using var fixture = new Harness();
		var warmupEntered = Signal();
		var allQueued = Signal();
		var claim = 0;
		fixture.Claim = async (_, _) =>
		{
			switch (Interlocked.Increment(ref claim))
			{
				case 1: return [CreateMessage("warmup")];
				case 2:
					await warmupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
					return [CreateMessage("A"), CreateMessage("B"), CreateMessage("C"), CreateMessage("later")];
				default: allQueued.TrySetResult(); return [];
			}
		};
		fixture.Dispatch = async (id, _) =>
		{
			if (id == "warmup") { warmupEntered.TrySetResult(); await allQueued.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
			return MessageResult.Success();
		};
		fixture.Mark = (id, _) => id == "warmup" ? ValueTask.FromResult(OutboxCompletionOutcome.Applied) : Refusal(thrown);
		await using var processor = fixture.Create(degree: 2, consumerBatch: 3, batchDatabase: batchDatabase);
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(4);
		fixture.Delivered.ShouldContain("warmup");
		fixture.Delivered.ShouldContain("A");
		fixture.Delivered.ShouldContain("B");
		fixture.Delivered.ShouldContain("C");
		fixture.Delivered.ShouldNotContain("later");
		fixture.Marked.Count(item => item.Id != "warmup").ShouldBe(1);
		fixture.AssertNoUnfencedCompletion();
	}

	[Fact]
	public async Task HonorCallerCancellationEvenWhenTheActiveTokenIsMissing()
	{
		await using var fixture = new Harness();
		fixture.Gate.SetToken(null);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await using var processor = fixture.Create();
		await Should.ThrowAsync<OperationCanceledException>(() => processor.DispatchPendingMessagesAsync(cancellation.Token));
		fixture.Claims.ShouldBe(0);
		fixture.Delivered.ShouldBeEmpty();
	}

	[Fact]
	public async Task RefuseAnActiveMissingTokenWithoutClaimingOrDowngradingToUnfencedTraffic()
	{
		await using var fixture = new Harness();
		fixture.Gate.SetToken(null);
		fixture.Enqueue("A");
		await using var processor = fixture.Create();
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		fixture.Claims.ShouldBe(0);
		fixture.Delivered.ShouldBeEmpty();
		A.CallTo(() => fixture.Store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		fixture.AssertNoUnfencedCompletion();
	}

	[Fact]
	public async Task RememberAThrownClaimRefusalBeforeARecreatedProcessorCanClaimAgain()
	{
		await using var fixture = new Harness();
		fixture.Claim = (_, _) => Task.FromException<IEnumerable<OutboundMessage>>(
			new StaleOutboxFencingTokenException("claim refused") { PresentedToken = 5, HighWaterToken = 6 });
		await using (var first = fixture.Create())
		{
			(await first.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		}
		fixture.Claims.ShouldBe(1);
		fixture.Claim = null;
		fixture.Enqueue("A");
		await using (var second = fixture.Create())
		{
			(await second.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		}
		fixture.Claims.ShouldBe(1);
		fixture.Delivered.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopAfterTheFencedDeadLetterTransitionRefusesTheTenure(bool thrown)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A", "B");
		fixture.Dispatch = (_, _) => Task.FromResult<IMessageResult>(MessageResult.Failed("controlled terminal failure"));
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<OutboxWriteAuthority>._, A<CancellationToken>._)).ReturnsLazily(() => Refusal(thrown));
		await using var processor = fixture.Create(maxAttempts: 1);
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		fixture.Delivered.ToArray().ShouldBe(["A"]);
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			"A", A<string>._, 1, A<OutboxWriteAuthority>.That.Matches(a => a.FencingToken == 5 && a.ClaimIdentity == "claim-A"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		fixture.AssertNoUnfencedCompletion();
	}

	[Fact]
	public async Task JoinAnOldRefusedCompletionWithoutPoisoningAnAlreadyAuthorizedNewerRun()
	{
		var gate = new TestGate();
		await using var old = new Harness(gate);
		await using var newer = new Harness(gate);
		var oldMarkEntered = Signal();
		var releaseOldMark = Signal();
		old.Enqueue("A");
		old.Mark = (_, token) => new ValueTask<OutboxCompletionOutcome>(RefuseOldAsync(token));
		async Task<OutboxCompletionOutcome> RefuseOldAsync(long token)
		{
			token.ShouldBe(5);
			oldMarkEntered.TrySetResult();
			await releaseOldMark.Task;
			return OutboxCompletionOutcome.FenceRefused;
		}
		await using var oldProcessor = old.Create();
		var oldRun = oldProcessor.DispatchPendingMessagesAsync(CancellationToken.None);
		try
		{
			await oldMarkEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			gate.SetToken(6);
			newer.Enqueue("C");
			await using (var firstNewer = newer.Create())
			{
				(await firstNewer.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
			}
			oldRun.IsCompleted.ShouldBeFalse();
		}
		finally
		{
			releaseOldMark.TrySetResult();
			_ = await Record.ExceptionAsync(() => oldRun.WaitAsync(TimeSpan.FromSeconds(10)));
		}
		(await oldRun.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		old.Marked.ToArray().ShouldBe([("A", 5L)]);
		newer.Enqueue("D");
		await using (var secondNewer = newer.Create())
		{
			(await secondNewer.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		}
		newer.Marked.ToArray().ShouldBe([("C", 6L), ("D", 6L)]);
	}

	[Theory]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	public async Task PreserveCapturedDeadLetterAuthorityAndCompensateRowRefusal(int degree, bool refused)
	{
		await using var fixture = new Harness();
		fixture.Enqueue("A");
		fixture.Dispatch = (_, _) => Task.FromResult<IMessageResult>(MessageResult.Failed("terminal failure"));
		var queue = A.Fake<IDeadLetterQueue>(o => o.Implements<IDeadLetterQueueAdmin>());
		var entry = Guid.NewGuid();
		A.CallTo(() => queue.EnqueueAsync(A<IOutboxMessage>._, A<DeadLetterReason>._, A<CancellationToken>._,
			A<Exception?>._, A<IDictionary<string, string>?>._))
			.ReturnsLazily((IOutboxMessage message, DeadLetterReason _, CancellationToken _, Exception? _, IDictionary<string, string>? _) =>
			{
				message.GetType().ShouldBe(typeof(Excalibur.Outbox.OutboxMessage), "private authority records must not change the public DLQ envelope type");
				message.MessageId.ShouldBe("A");
				message.Attempts.ShouldBe(1);
				message.DispatcherId = "rewritten-by-dlq-callback";
				return Task.FromResult(entry);
			});
		A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(entry, A<CancellationToken>._)).Returns(Task.FromResult(true));
		var observed = new ConcurrentQueue<OutboxWriteAuthority>();
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			"A", A<string>._, 1, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily((string _, string _, int _, OutboxWriteAuthority authority, CancellationToken _) =>
			{
				observed.Enqueue(authority);
				return ValueTask.FromResult(refused ? OutboxCompletionOutcome.ClaimLost : OutboxCompletionOutcome.Applied);
			});
		await using var processor = fixture.Create(degree: degree, maxAttempts: 1, deadLetterQueue: queue);
		(await processor.DispatchPendingMessagesAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);
		observed.ToArray().ShouldBe(new[] { new OutboxWriteAuthority(5, "claim-A") });
		fixture.Delivered.ToArray().ShouldBe(["A"]);
		if (refused) { A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(entry, A<CancellationToken>._)).MustHaveHappenedOnceExactly(); }
		else { A.CallTo(() => ((IDeadLetterQueueAdmin)queue).PurgeAsync(entry, A<CancellationToken>._)).MustNotHaveHappened(); }
		A.CallTo(() => ((IFencedDeadLetterableOutboxStore)fixture.Store).MarkDeadLetteredAsync(
			A<string>._, A<string>._, A<int>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
		fixture.AssertNoUnfencedCompletion();
	}

	private static ValueTask<OutboxCompletionOutcome> Refusal(bool thrown)
	{
		if (thrown) { throw new StaleOutboxFencingTokenException("controlled stale tenure") { PresentedToken = 5, HighWaterToken = 6 }; }
		return ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
	}

	private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static OutboundMessage CreateMessage(string id) => new()
	{
		Id = id, MessageType = typeof(TenureEvent).FullName!, DispatcherId = "claim-" + id,
		Payload = JsonSerializer.SerializeToUtf8Bytes(new TenureEvent(id), JsonOptions),
		Headers = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["MessageId"] = id, ["CorrelationId"] = "tenure-control", ["ContentType"] = "application/json",
			["SerializerVersion"] = "1.0.0", ["MessageVersion"] = "1.0.0",
		},
	};

	internal sealed record TenureEvent(string Value) : IIntegrationEvent;

	private class TestGate : ILeaderProcessingGate
	{
		private long _token = 5;
		public bool ShouldProcess => true;
		public long? FencingToken => Interlocked.Read(ref _token) is var value && value >= 0 ? value : null;
		internal void SetToken(long? value) => Interlocked.Exchange(ref _token, value ?? -1);
		public override bool Equals(object? obj) => obj is TestGate;
		public override int GetHashCode() => 0;
	}

	private sealed class DomainGate(object domain) : TestGate, ILeaderProcessingAuthorityDomain
	{
		public object AuthorityDomain { get; } = domain;
	}

	private sealed class RefusalMilestoneLogger(int refusalEventId, TaskCompletionSource milestone) : ILogger<OutboxProcessor>
	{
		private int _observed;
		internal int ObservedRefusals => Volatile.Read(ref _observed);
		public bool IsEnabled(LogLevel logLevel) => true;
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (eventId.Id != refusalEventId) { return; }
			Interlocked.Increment(ref _observed);
			// These exact warnings are emitted after production records the typed/thrown refusal.
			// The fake store's return is not that milestone: it precedes admission-state publication.
			milestone.TrySetResult();
		}
	}

	private sealed class BatchFailureObserver : IDisposable
	{
		private readonly MeterListener _listener;
		private Meter? _meter;
		private int _captureThread;
		private long _failures;
		private long _completed;
		internal long Failures => Interlocked.Read(ref _failures);
		internal long CompletedBatches => Interlocked.Read(ref _completed);

		internal BatchFailureObserver()
		{
			_listener = new MeterListener
			{
				InstrumentPublished = (instrument, listener) =>
				{
					if (Volatile.Read(ref _captureThread) != Environment.CurrentManagedThreadId
						|| instrument.Meter.Name != "OutboxProcessor.BatchProcessingMetrics") { return; }
					_meter ??= instrument.Meter;
					if (ReferenceEquals(_meter, instrument.Meter)) { listener.EnableMeasurementEvents(instrument); }
				},
			};
			_listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
			{
				if (!ReferenceEquals(_meter, instrument.Meter)) { return; }
				if (instrument.Name == "dispatch.batch.messages.failed") { Interlocked.Add(ref _failures, measurement); }
				if (instrument.Name == "dispatch.batch.batches.processed") { Interlocked.Add(ref _completed, measurement); }
			});
			_listener.Start();
		}

		internal OutboxProcessor Capture(Func<OutboxProcessor> create)
		{
			Volatile.Write(ref _captureThread, Environment.CurrentManagedThreadId);
			try { return create(); }
			finally { Volatile.Write(ref _captureThread, 0); }
		}

		public void Dispose() => _listener.Dispose();
	}

	private sealed class Harness : IAsyncDisposable
	{
		internal readonly TestGate Gate;
		internal readonly IOutboxStore Store;
		internal readonly ConcurrentQueue<string> Delivered = new();
		internal readonly ConcurrentQueue<(string Id, long Token)> Marked = new();
		private readonly ConcurrentQueue<OutboundMessage[]> _batches = new();
		private readonly ServiceProvider _services;
		private int _claims;
		internal int Claims => Volatile.Read(ref _claims);
		internal Func<string, CancellationToken, Task<IMessageResult>> Dispatch = (_, _) => Task.FromResult<IMessageResult>(MessageResult.Success());
		internal Func<string, long, ValueTask<OutboxCompletionOutcome>> Mark = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		internal Func<int, CancellationToken, Task<IEnumerable<OutboundMessage>>>? Claim;
		internal ILogger<OutboxProcessor> Logger = NullLogger<OutboxProcessor>.Instance;

		internal Harness(TestGate? gate = null)
		{
			Gate = gate ?? new TestGate();
			MessageTypeRegistry.RegisterType<TenureEvent>();
			Store = CapabilityHonouringFakes.OutboxStore(fake => fake.Implements<IFencedOutboxStore>()
				.Implements<IFencedClaimScopedOutboxStore>().Implements<IFencedDeadLetterableOutboxStore>()
				.Implements<IDeadLetterableOutboxStore>());
			A.CallTo(() => ((IFencedOutboxStore)Store).GetUnsentMessagesAsync(A<int>._, A<long>._, A<CancellationToken>._))
				.ReturnsLazily(call => new ValueTask<IEnumerable<OutboundMessage>>(ReadAsync(call.GetArgument<int>(0), call.GetArgument<CancellationToken>(2))));
			A.CallTo(() => ((IFencedClaimScopedOutboxStore)Store).MarkSentAsync(A<string>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily(call =>
				{
					var id = call.GetArgument<string>(0)!;
					var token = call.GetArgument<OutboxWriteAuthority>(1).FencingToken;
					Marked.Enqueue((id, token));
					return Mark(id, token);
				});
			var dispatcher = A.Fake<IDispatcher>();
			A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
				.ReturnsLazily(call =>
				{
					var message = call.GetArgument<IDispatchMessage>(0).ShouldBeOfType<TenureEvent>();
					var context = call.GetArgument<IMessageContext>(1)!;
					context.MessageId.ShouldBe(message.Value, "real payload decoding and persisted identity are positive preconditions");
					Delivered.Enqueue(message.Value);
					return Dispatch(message.Value, call.GetArgument<CancellationToken>(2));
				});
			_services = new ServiceCollection().AddSingleton(dispatcher).BuildServiceProvider();
		}

		private Task<IEnumerable<OutboundMessage>> ReadAsync(int count, CancellationToken token)
		{
			Interlocked.Increment(ref _claims);
			if (Claim is not null) { return Claim(count, token); }
			return Task.FromResult<IEnumerable<OutboundMessage>>(_batches.TryDequeue(out var rows) ? rows : []);
		}

		internal void Enqueue(params string[] ids) => _batches.Enqueue(ids.Select(CreateMessage).ToArray());

		internal OutboxProcessor Create(int degree = 1, int consumerBatch = 1, bool minimized = false, bool batchDatabase = false, int maxAttempts = 3, IDeadLetterQueue? deadLetterQueue = null)
		{
			var options = new DeliveryOptions
			{
				QueueCapacity = 16, ProducerBatchSize = 8, ConsumerBatchSize = consumerBatch, PerRunTotal = 16, MaxAttempts = maxAttempts,
				EnableBatchDatabaseOperations = batchDatabase,
				DeliveryGuarantee = minimized ? OutboxDeliveryGuarantee.MinimizedWindow : OutboxDeliveryGuarantee.AtLeastOnce,
				BatchProcessing = { ParallelProcessingDegree = degree, EnableDynamicBatchSizing = false },
			};
			var processor = new OutboxProcessor(Options.Create(options), Store, new DispatchJsonSerializer(), _services,
				Logger, PassThroughCircuitBreakerRegistry.Instance,
				deadLetterQueue: deadLetterQueue ?? NullDeadLetterQueue.Instance, leaderGate: Gate);
			processor.Init("tenure-controls");
			return processor;
		}

		internal void AssertNoUnfencedCompletion()
		{
			A.CallTo(() => Store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => Store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => ((IDeadLetterableOutboxStore)Store).MarkDeadLetteredAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		}

		public ValueTask DisposeAsync() => _services.DisposeAsync();
	}
}
