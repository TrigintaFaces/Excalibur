// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class OutboxAcceptedCompletionFailureShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveAcceptanceAndStopOnlyThisOperationWhenDurableCompletionThrows(bool cancellationFault)
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		var rows = h.Rows("accepted", "must-not-send");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		Exception failure = cancellationFault ? new OperationCanceledException("completion fault") : new IOException("completion commit unknown");
		h.Complete = (_, _) => ValueTask.FromException<OutboxCompletionOutcome>(failure);

		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);

		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		result.SkippedCount.ShouldBe(1);
		result.Outcome.ShouldBe(PublishingOutcome.Interrupted);
		result.IsSuccess.ShouldBeFalse();
		result.IsTenureRefused.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].MessageId.ShouldBe(rows[0].Id);
		result.Errors[0].Exception.ShouldBeSameAs(failure);
		h.Published.ShouldBe(new[] { rows[0].Id });
		h.FailureTokens.ShouldBeEmpty();
		h.AssertNoUnfencedWrites();

		// An infrastructure fault is not evidence that tenure7 was superseded. A later operation
		// under that same gate can be admitted, without erasing or reclassifying the first failure.
		var next = h.Rows("next-operation");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(next);
		h.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		var resumed = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		resumed.IsSuccess.ShouldBeTrue();
		resumed.SuccessCount.ShouldBe(1);
		h.Published.ShouldBe(new[] { rows[0].Id, next[0].Id });
		h.FailureTokens.ShouldBeEmpty();
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepPreAcceptanceTransportFaultAsDeliveryFailure(bool cancellationFault)
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(h.Rows("failed"));
		Exception failure = cancellationFault ? new OperationCanceledException("transport canceled independently") : new IOException("transport failed");
		h.Send = _ => Task.FromException<IMessageResult>(failure);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		result.Outcome.ShouldBe(PublishingOutcome.Completed);
		result.Errors.Single().Exception.ShouldBeSameAs(failure);
		h.CompletionTokens.ShouldBeEmpty();
		h.FailureTokens.ShouldBe(new long[] { 7 });
	}

	[Fact]
	public async Task PropagateRunCancellationBeforeAcceptanceWithoutCompletionOrFailureReport()
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		using var cancellation = new CancellationTokenSource();
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(h.Rows("canceled"));
		h.Send = _ =>
		{
			cancellation.Cancel();
			return Task.FromCanceled<IMessageResult>(cancellation.Token);
		};
		await Should.ThrowAsync<OperationCanceledException>(() => h.Publisher.PublishPendingMessagesAsync(cancellation.Token));
		h.CompletionTokens.ShouldBeEmpty();
		h.FailureTokens.ShouldBeEmpty();
	}

}
