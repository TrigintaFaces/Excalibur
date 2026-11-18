// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class MessageBusOutboxTenureShould
{
	private static readonly string[] MultiTransportDestinations = ["one", "two"];
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopAfterReportedOrThrownCompletionRefusalAndRetainPriorCounters(bool thrown)
	{
		var h = new Harness();
		var rows = h.Rows("completed", "refused", "must-not-send");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Complete = (id, _) => id == rows[1].Id
			? thrown ? ValueTask.FromException<OutboxCompletionOutcome>(Stale(7))
				: ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused)
			: ValueTask.FromResult(OutboxCompletionOutcome.Applied);

		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);

		h.Published.ShouldBe(new[] { rows[0].Id, rows[1].Id });
		result.SuccessCount.ShouldBe(2); // Both actual transport acceptances, not a durable-completion claim.
		result.FailureCount.ShouldBe(0);
		result.IsSuccess.ShouldBeFalse();
		result.SkippedCount.ShouldBe(1);
		h.Publisher.GetStatistics().TotalMessagesPublished.ShouldBe(2);
		h.AssertNoUnfencedWrites();
	}

	[Fact]
	public async Task RememberRefusalAcrossScheduledRetryAndPollingWithoutResetOnAbsentGate()
	{
		var h = new Harness();
		var rows = h.Rows("a", "b");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await h.Publisher.PublishScheduledMessagesAsync(CancellationToken.None);
		await h.Publisher.RetryFailedMessagesAsync(5, CancellationToken.None);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.Token = null;
		await Should.ThrowAsync<OutboxFencingTokenUnavailableException>(() => h.Publisher.PublishPendingMessagesAsync(CancellationToken.None));
		h.Token = 7;
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7 });
		h.Published.ShouldBe(new[] { rows[0].Id });

		h.Token = 8;
		h.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7, 8 });
		h.CompletionTokens.TakeLast(2).ShouldBe(new long[] { 8, 8 });
	}

	[Theory]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(OutboxCompletionOutcome.MessageNotFound)]
	public async Task ContinueOtherRowsAfterRowScopedRefusal(OutboxCompletionOutcome outcome)
	{
		var h = new Harness();
		var rows = h.Rows("a", "b");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Complete = (id, _) => ValueTask.FromResult(id == rows[0].Id ? outcome : OutboxCompletionOutcome.Applied);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.Published.ShouldBe(rows.Select(r => r.Id));
		result.SuccessCount.ShouldBe(2);
		result.IsSuccess.ShouldBeTrue();
		h.AssertNoUnfencedWrites();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopAfterTypedOrThrownFencedFailureReportRefusal(bool thrown)
	{
		var h = new Harness();
		var rows = h.Rows("a", "must-not-send");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Send = _ => Task.FromException<IMessageResult>(new IOException("transport failure"));
		h.Fail = _ => thrown ? ValueTask.FromException<OutboxCompletionOutcome>(Stale(7))
			: ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.Published.ShouldBe(new[] { rows[0].Id });
		h.FailureTokens.ShouldBe(new long[] { 7 });
		result.FailureCount.ShouldBe(1);
		result.SuccessCount.ShouldBe(0);
		h.AssertNoUnfencedWrites();
		await h.Publisher.PublishScheduledMessagesAsync(CancellationToken.None);
		h.ClaimTokens.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RememberThrownClaimRefusalButDoNotInferRefusalFromEmptyClaim()
	{
		var h = new Harness();
		h.Claim = _ => ValueTask.FromException<IEnumerable<OutboundMessage>>(Stale(7));
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await h.Publisher.PublishScheduledMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7 });
		h.Token = 8;
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7, 8, 8 });
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverRelabelAnOldClaimWithANewerLiveToken(bool failure)
	{
		var h = new Harness();
		var rows = h.Rows("a", "must-not-send");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		h.Send = async _ =>
		{
			entered.TrySetResult();
			await release.Task;
			if (failure) { throw new IOException("transport failed after handover"); }
			return MessageResult.Success();
		};
		var run = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			h.Token = 8;
		}
		finally
		{
			release.TrySetResult();
			await run;
		}
		(failure ? h.FailureTokens : h.CompletionTokens).ShouldBe(new long[] { 7 });
		h.Published.ShouldBe(new[] { rows[0].Id });
		h.AssertNoUnfencedWrites();
	}

	[Fact]
	public async Task JoinAnAdmittedSendWhileConcurrentRefusalBlocksLaterAdmissionsAndAllowsANewerTenure()
	{
		var h = new Harness();
		var rows = h.Rows("held", "refusing", "later", "newer");
		var next = 0;
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([rows[Interlocked.Increment(ref next) - 1]]);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		h.Send = async id =>
		{
			if (id == rows[0].Id) { entered.TrySetResult(); await release.Task; }
			return MessageResult.Success();
		};
		h.Complete = (id, _) => ValueTask.FromResult(id == rows[1].Id ? OutboxCompletionOutcome.FenceRefused : OutboxCompletionOutcome.Applied);
		var held = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			await h.Publisher.PublishScheduledMessagesAsync(CancellationToken.None);
			await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
			h.ClaimTokens.Count.ShouldBe(2);
			held.IsCompleted.ShouldBeFalse();
			h.Token = 8;
			await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
			h.ClaimTokens.ShouldBe(new long[] { 7, 7, 8 });
		}
		finally { release.TrySetResult(); await held; }
		h.CompletionTokens.Last().ShouldBe(7); // Late T completion never borrows T2.
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveUnconfiguredAndExplicitSingleWriterModes(bool singleWriter)
	{
		var h = new Harness(configured: singleWriter, singleWriter: singleWriter);
		var rows = h.Rows("a", "b");
		A.CallTo(() => h.Store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).Returns(rows);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(2);
		h.ClaimTokens.ShouldBeEmpty();
		A.CallTo(() => ((IClaimScopedOutboxStore)h.Store).MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
		A.CallTo(() => h.Store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ShareLearnedRefusalAcrossTwoPublishersWithTheSameGate()
	{
		var gate = new EqualGate();
		var first = new Harness(sharedGate: gate);
		var second = new Harness(sharedGate: gate);
		first.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(first.Rows("a"));
		second.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(second.Rows("b"));
		first.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await first.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await second.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		second.ClaimTokens.ShouldBeEmpty();
		second.Published.ShouldBeEmpty();
		gate.Token = 8;
		var current = await second.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		current.SuccessCount.ShouldBe(1);
		second.ClaimTokens.ShouldBe(new long[] { 8 });
	}

	[Fact]
	public async Task IsolateDistinctEqualGatesWithIdenticalNumericTokens()
	{
		var firstGate = new EqualGate();
		var secondGate = new EqualGate();
		firstGate.Equals(secondGate).ShouldBeTrue();
		var first = new Harness(sharedGate: firstGate);
		var second = new Harness(sharedGate: secondGate);
		first.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(first.Rows("a"));
		second.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(second.Rows("b"));
		first.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await first.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		var independent = await second.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		independent.SuccessCount.ShouldBe(1);
		second.ClaimTokens.ShouldBe(new long[] { 7 });
	}

	private sealed class EqualGate : ILeaderProcessingGate
	{
		internal long Token = 7;
		public bool ShouldProcess => true;
		public long? FencingToken => Token;
		public override bool Equals(object? obj) => obj is EqualGate;
		public override int GetHashCode() => 0;
	}

	private static StaleOutboxFencingTokenException Stale(long token) => new("newer tenure")
	{
		PresentedToken = token, HighWaterToken = token + 1,
	};

	[Fact]
	public async Task LateRefusalForOldTokenDoesNotPoisonAnAuthorizedNewerToken()
	{
		var h = new Harness();
		var rows = h.Rows("old", "new");
		h.Claim = token => ValueTask.FromResult<IEnumerable<OutboundMessage>>([rows[token == 7 ? 0 : 1]]);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		h.Complete = async (_, token) =>
		{
			if (token == 7) { entered.TrySetResult(); await release.Task; return OutboxCompletionOutcome.FenceRefused; }
			return OutboxCompletionOutcome.Applied;
		};
		var old = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			h.Token = 8;
			await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		}
		finally { release.TrySetResult(); await old; }
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7, 8, 8 });
		h.CompletionTokens.ShouldBe(new long[] { 7, 8, 8 });
	}

	[Fact]
	public async Task MissingTokenGuardDoesNotInventARejectedWatermark()
	{
		var h = new Harness();
		h.Claim = _ => ValueTask.FromException<IEnumerable<OutboundMessage>>(new OutboxFencingTokenUnavailableException("guard"));
		var refused = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		refused.IsSuccess.ShouldBeFalse();
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		h.ClaimTokens.ShouldBe(new long[] { 7, 7 });
	}

	[Fact]
	public async Task JoinAnAdmittedFanoutBeforeReportingALaterRegistryFailure()
	{
		_ = await RunRegistryFailureAsync(changeTenure: false);
	}

	internal static async Task<PublishingResult> RunRegistryFailureAsync(bool changeTenure)
	{
		var h = new Harness(multi: true);
		var row = h.Rows("a")[0];
		row.IsMultiTransport = true;
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "one"));
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "two"));
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var lookupFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var failure = new IOException("second adapter lookup failed");
		A.CallTo(() => h.Registry.GetTransportAdapter(A<string>._)).ReturnsLazily((string name) =>
		{
			if (name == "one") { return h.Adapter; }
			if (changeTenure) { h.Token = 8; }
			lookupFailed.TrySetResult();
			throw failure;
		});
		A.CallTo(() => h.Adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async () => { entered.TrySetResult(); await release.Task; });
		var run = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			await lookupFailed.Task.WaitAsync(TimeSpan.FromSeconds(10));
			run.IsCompleted.ShouldBeFalse();
		}
		finally { release.TrySetResult(); await run; }
		var result = await run;
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		result.IsSuccess.ShouldBeFalse();
		result.Errors.ShouldContain(e => ReferenceEquals(e.Exception, failure));
		A.CallTo(() => ((IMultiTransportOutboxStore)h.Store).MarkTransportSentAsync(row.Id, "one", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		return result;
	}

	[Fact]
	public async Task PublishRefusalBeforeAConcurrentBlockedGateObservationCanAdmitAClaim()
	{
		var h = new Harness();
		var row = h.Rows("a")[0];
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		var markEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var markRelease = new TaskCompletionSource<OutboxCompletionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
		h.Complete = (_, _) => { markEntered.TrySetResult(); return new ValueTask<OutboxCompletionOutcome>(markRelease.Task); };
		var first = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		using var gateRelease = new ManualResetEventSlim();
		var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task<PublishingResult>? later = null;
		try
		{
			await markEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			h.Allow = () => { gateEntered.TrySetResult(); gateRelease.Wait(); return true; };
			later = Task.Run(() => h.Publisher.PublishPendingMessagesAsync(CancellationToken.None));
			await gateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			markRelease.TrySetResult(OutboxCompletionOutcome.FenceRefused);
			await first.WaitAsync(TimeSpan.FromSeconds(10)); // Gate getters cannot hold the watermark lock.
		}
		finally
		{
			gateRelease.Set();
			markRelease.TrySetResult(OutboxCompletionOutcome.FenceRefused);
			await first;
			if (later is not null) { await later; }
		}
		h.ClaimTokens.ShouldBe(new long[] { 7 });
		h.Published.ShouldBe(new[] { row.Id });
	}

	[Fact]
	public async Task RefuseAllMultiTransportEntrypointsAfterLearningATenureRefusal()
	{
		var h = new Harness(multi: true);
		var row = h.Rows("a")[0];
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		// No single-transport adapter in this public composition: its real delivery
		// failure exercises the fenced failure-report route, which refuses the tenure.
		h.Fail = _ => ValueTask.FromResult(OutboxCompletionOutcome.FenceRefused);
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		row.IsMultiTransport = true;
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "one"));
		await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		await h.Publisher.PublishPendingTransportDeliveriesAsync("one", CancellationToken.None);
		h.ClaimTokens.Count.ShouldBe(1);
		A.CallTo(() => h.Registry.GetTransportAdapter(A<string>._)).MustNotHaveHappened();
		A.CallTo(() => h.Adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task JoinEveryAdmittedFanoutAndRetainActualTransportFailureDetails(bool changeTenure)
	{
		var h = new Harness(multi: true);
		var row = h.Rows("a")[0];
		row.IsMultiTransport = true;
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "one") { Destination = "one" });
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "two") { Destination = "two" });
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var sends = new List<string>();
		var failure = new IOException("adapter two failed");
		A.CallTo(() => h.Adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(async (IDispatchMessage _, string destination, IMessageContext _, CancellationToken _) =>
			{
				sends.Add(destination);
				if (destination == "one") { entered.TrySetResult(); await release.Task; }
				else { throw failure; }
			});
		var run = h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			sends.ShouldBe(MultiTransportDestinations);
			run.IsCompleted.ShouldBeFalse();
			if (changeTenure) { h.Token = 8; }
		}
		finally { release.TrySetResult(); await run; }
		var result = await run;
		result.SuccessCount.ShouldBe(1); // One partially delivered message, not two rows.
		result.FailureCount.ShouldBe(0);
		if (changeTenure)
		{
			result.IsSuccess.ShouldBeFalse();
			result.Errors.ShouldContain(e => ReferenceEquals(e.Exception, failure));
			A.CallTo(() => ((IMultiTransportOutboxStoreAdmin)h.Store).UpdateAggregateStatusAsync(row.Id, A<CancellationToken>._))
				.MustNotHaveHappened();
		}
		else
		{
			result.IsSuccess.ShouldBeTrue();
			A.CallTo(() => ((IMultiTransportOutboxStoreAdmin)h.Store).UpdateAggregateStatusAsync(row.Id, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}
	}

	internal sealed class Harness
	{
		internal readonly IOutboxStore Store = A.Fake<IOutboxStore>(o => o
			.Implements<IFencedOutboxStore>().Implements<IFencedClaimScopedOutboxStore>().Implements<IClaimScopedOutboxStore>()
			.Implements<IMultiTransportOutboxStore>().Implements<IMultiTransportOutboxStoreAdmin>()).WithHonestCapabilities();
		internal readonly ITransportRegistry Registry = A.Fake<ITransportRegistry>();
		internal readonly ITransportAdapter Adapter = A.Fake<ITransportAdapter>();
		internal readonly List<long> ClaimTokens = [];
		internal readonly List<long> CompletionTokens = [];
		internal readonly List<long> FailureTokens = [];
		internal readonly List<string> Published = [];
		internal long? Token = 7;
		internal Func<bool> Allow = () => true;
		internal Func<long, ValueTask<IEnumerable<OutboundMessage>>> Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([]);
		internal Func<string, long, ValueTask<OutboxCompletionOutcome>> Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		internal Func<long, ValueTask<OutboxCompletionOutcome>> Fail = _ => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		internal Func<string, Task<IMessageResult>> Send = _ => Task.FromResult<IMessageResult>(MessageResult.Success());
		internal readonly MessageBusOutboxPublisher Publisher;

		internal Harness(bool configured = true, bool singleWriter = false, bool multi = false, ILeaderProcessingGate? sharedGate = null)
		{
			var gate = sharedGate ?? A.Fake<ILeaderProcessingGate>();
			if (sharedGate is null)
			{
				A.CallTo(() => gate.ShouldProcess).ReturnsLazily(() => Allow());
				A.CallTo(() => gate.FencingToken).ReturnsLazily(() => Token);
			}
			var provider = A.Fake<IServiceProvider>();
			A.CallTo(() => provider.GetService(A<Type>._)).ReturnsLazily((Type t) =>
				t == typeof(ILeaderProcessingGate) && configured ? gate : t == typeof(IOptions<OutboxDeliveryOptions>)
					? Microsoft.Extensions.Options.Options.Create(new OutboxDeliveryOptions { SingleActiveWriter = singleWriter }) : null);
			A.CallTo(() => ((IFencedOutboxStore)Store).GetUnsentMessagesAsync(A<int>._, A<long>._, A<CancellationToken>._))
				.ReturnsLazily((int _, long token, CancellationToken _) => { ClaimTokens.Add(token); return Claim(token); });
			A.CallTo(() => ((IFencedClaimScopedOutboxStore)Store).MarkSentAsync(A<string>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily((string id, OutboxWriteAuthority authority, CancellationToken _) => { CompletionTokens.Add(authority.FencingToken); return Complete(id, authority.FencingToken); });
			A.CallTo(() => ((IClaimScopedOutboxStore)Store).MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, CancellationToken _) => Complete(id, 0));
			A.CallTo(() => ((IFencedClaimScopedOutboxStore)Store).MarkFailedAsync(A<string>._, A<string>._, A<int>._,
				A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
				.ReturnsLazily((string _, string _, int _, DateTimeOffset? _, OutboxWriteAuthority authority, CancellationToken _) =>
				{ FailureTokens.Add(authority.FencingToken); return Fail(authority.FencingToken); });
			var bus = A.Fake<IMessageBusAdapter>();
			A.CallTo(() => bus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
				.ReturnsLazily((IDispatchMessage _, IMessageContext context, CancellationToken _) =>
				{ Published.Add(context.MessageId!); return Send(context.MessageId!); });
			A.CallTo(() => Registry.GetTransportAdapter(A<string>._)).Returns(Adapter);
			Publisher = multi ? new MessageBusOutboxPublisher(Store, OutboxFakeCapabilityExtensions.CreateRegisteredSerializer(), Registry,
				provider, A.Fake<ILogger<MessageBusOutboxPublisher>>())
				: new MessageBusOutboxPublisher(Store, OutboxFakeCapabilityExtensions.CreateRegisteredSerializer(), bus,
					provider, A.Fake<ILogger<MessageBusOutboxPublisher>>());
		}

		internal OutboundMessage[] Rows(params string[] destinations) => destinations.Select(destination =>
			new OutboundMessage(OutboxFakeCapabilityExtensions.RegisteredMessageType, "body"u8.ToArray(), destination)
			{ DispatcherId = "claim-" + Token + "-" + destination }).ToArray();

		internal void AssertNoUnfencedWrites()
		{
			A.CallTo(() => Store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => Store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}
}
