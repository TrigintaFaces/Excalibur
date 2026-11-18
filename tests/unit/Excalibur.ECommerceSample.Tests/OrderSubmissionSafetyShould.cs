// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Excalibur.ECommerceSample.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Sample.OrderSubmission")]
[Trait("Pattern", "Regression")]
public sealed class OrderSubmissionSafetyShould
{
	[Fact]
	public async Task RefuseChangedBusinessInputsOnTheCompletedDuplicatePath()
	{
		await using var stores = Stores();
		var repository = Repository();
		var processor = Processor(stores.GetRequiredService<IInboxStore>(), stores.GetRequiredService<IOutboxStore>(), repository);
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.Processed);
		(await processor.ProcessOrderAsync(Order() with { Price = 99m }, default)).ShouldBe(OrderProcessingOutcome.Conflict);
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.Duplicate);
		repository.GetTotalOrderCount().ShouldBe(1);
	}

	[Fact]
	public async Task KeepOneStoredIntentAndStopAutomaticRedriveAfterAnUnknownStageAcknowledgement()
	{
		await using var stores = Stores();
		var real = stores.GetRequiredService<IOutboxStore>();
		var proxy = A.Fake<IOutboxStore>();
		var calls = 0;
		A.CallTo(() => proxy.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.ReturnsLazily((OutboundMessage message, CancellationToken token) => new ValueTask(Stage(message, token)));
		async Task Stage(OutboundMessage message, CancellationToken token)
		{
			Interlocked.Increment(ref calls);
			await real.StageMessageAsync(message, token);
			throw new IOException("acknowledgement-lost-after-real-store-write");
		}
		var processor = Processor(stores.GetRequiredService<IInboxStore>(), proxy, Repository());
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.Failed);
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.NeedsReconciliation);
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.NeedsReconciliation);
		calls.ShouldBe(2);
		(await real.GetUnsentMessagesAsync(10, default)).Count().ShouldBe(1);
	}

	[Fact]
	public void AStaleReadySnapshotCannotTakeASuccessorsPublicationPermit()
	{
		var repository = Repository();
		var ready = repository.Capture(new OrderSubmissionPlan(Order()));
		repository.BeginPublication(ready, out var successor).ShouldBeTrue();
		repository.BeginPublication(ready, out _).ShouldBeFalse();
		repository.RecordPublication(successor, accepted: true, error: null).ShouldBeTrue();
		repository.RecordPublication(successor, accepted: false, error: "late-error").ShouldBeFalse();
		repository.Submission(Order().OrderId).ShouldNotBeNull().Publication.ShouldBe(PublicationState.Accepted);
	}

	[Fact]
	public async Task KeepAnInFlightPermitAcrossInboxReclaimAndLetAFreshTermCompleteWithoutResending()
	{
		await using var stores = Stores();
		var real = stores.GetRequiredService<IOutboxStore>();
		var proxy = A.Fake<IOutboxStore>();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var calls = 0;
		A.CallTo(() => proxy.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.ReturnsLazily((OutboundMessage message, CancellationToken token) => new ValueTask(Stage(message, token)));
		async Task Stage(OutboundMessage message, CancellationToken token)
		{
			Interlocked.Increment(ref calls);
			entered.TrySetResult();
			await release.Task;
			await real.StageMessageAsync(message, token);
		}
		var inbox = A.Fake<IInboxStore>(options => options.Implements<ILeasedInboxStore>());
		InboxEntry? retainedEntry = null;
		A.CallTo(() => inbox.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() => ValueTask.FromResult<InboxEntry?>(retainedEntry));
		A.CallTo(() => inbox.CreateEntryAsync(A<string>._, A<string>._, A<string>._, A<byte[]>._,
			A<IDictionary<string, object>>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handler, string type, byte[] payload, IDictionary<string, object> metadata, CancellationToken _) =>
			{
				if (retainedEntry != null)
				{
					throw new InvalidOperationException("The fixture inbox identity already exists.");
				}

				retainedEntry = new InboxEntry(id, handler, type, (byte[])payload.Clone(), new Dictionary<string, object>(metadata));
				return ValueTask.FromResult<InboxEntry>(retainedEntry);
			});
		var leased = (ILeasedInboxStore)inbox;
		var acquisitions = 0;
		var current = string.Empty;
		A.CallTo(() => leased.TryAcquireLeaseAsync(A<string>._, A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.ReturnsLazily(() => { current = (++acquisitions).ToString(System.Globalization.CultureInfo.InvariantCulture);
				return ValueTask.FromResult<LeaseToken?>(new LeaseToken(current)); });
		A.CallTo(() => leased.CompleteAsync(A<string>._, A<string>._, A<LeaseToken>._, A<CancellationToken>._))
			.ReturnsLazily((string _, string _, LeaseToken term, CancellationToken _) => ValueTask.FromResult(term.Value == current));
		A.CallTo(() => leased.FailAsync(A<string>._, A<string>._, A<LeaseToken>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string _, string _, LeaseToken term, string _, CancellationToken _) => ValueTask.FromResult(term.Value == current));
		var processor = Processor(inbox, proxy, Repository());
		var first = processor.ProcessOrderAsync(Order(), default);
		OrderProcessingOutcome firstOutcome;
		try
		{
			var reached = await Task.WhenAny(entered.Task, first).WaitAsync(TimeSpan.FromSeconds(10));
			if (reached == first)
			{
				var earlyOutcome = await first;
				throw new InvalidOperationException($"The first attempt returned {earlyOutcome} before entering the publication gate.");
			}
			(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.Pending);
			calls.ShouldBe(1);
		}
		finally
		{
			release.TrySetResult();
			firstOutcome = await first.WaitAsync(TimeSpan.FromSeconds(10));
		}
		firstOutcome.ShouldBe(OrderProcessingOutcome.Pending);
		(await processor.ProcessOrderAsync(Order(), default)).ShouldBe(OrderProcessingOutcome.Processed);
		calls.ShouldBe(1);
		(await real.GetUnsentMessagesAsync(10, default)).Count().ShouldBe(1);
	}

	[Fact]
	public void CapturedIntentOwnsEveryReturnedPayloadAndMetadataBag()
	{
		var plan = new OrderSubmissionPlan(Order());
		var first = plan.CreateMessage();
		var expected = first.Payload.ToArray();
		first.Payload[0] ^= 1;
		first.Headers["businessFingerprint"] = "forged";
		var second = plan.CreateMessage();
		second.Payload.ShouldBe(expected);
		second.Headers["businessFingerprint"].ShouldBe(plan.Fingerprint);
		second.Headers.ShouldNotBeSameAs(first.Headers);
	}

	[Fact]
	public async Task RefuseTheReliableProfileWhenOnlyVolatileCapabilitiesAreRegistered()
	{
		await using var stores = Stores();
		var opened = false;
		Should.Throw<InvalidOperationException>(() => new SqlOrderSubmissionService(
			stores.GetRequiredService<IInboxStore>(), stores.GetRequiredService<IOutboxStore>(),
			() => { opened = true; throw new InvalidOperationException("must-not-open"); }));
		opened.ShouldBeFalse();
	}

	private static OrderProcessingService Processor(IInboxStore inbox, IOutboxStore outbox, InMemoryOrderRepository repository)
	{
		var monitor = new PerformanceMonitor();
		return new(inbox, repository, new NotificationService(outbox, monitor, NullLogger<NotificationService>.Instance),
			monitor, NullLogger<OrderProcessingService>.Instance);
	}
	private static InMemoryOrderRepository Repository() => new(NullLogger<InMemoryOrderRepository>.Instance);
	private static ServiceProvider Stores()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddExcaliburInbox(inbox => inbox.UseInMemory());
		services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseInMemory()));
		return services.BuildServiceProvider();
	}
	private static OrderCreated Order() => new()
	{
		OrderId = "d06-safety", CustomerId = "customer@example.test", ProductId = "product",
		ProductName = "Product", Price = 10m, Quantity = 1,
		OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
	};
}
