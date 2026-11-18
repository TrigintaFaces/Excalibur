// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class OutboxCompletionOutcomeAdmissionShould
{
	[Fact]
	public async Task InterruptOnlyThisOperationForAnUnknownCompletionWithoutFabricatingTenureRefusal()
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		var rows = h.Rows("accepted", "must-not-send");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Complete = (_, _) => ValueTask.FromResult((OutboxCompletionOutcome)999);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		result.SkippedCount.ShouldBe(1);
		result.Outcome.ShouldBe(PublishingOutcome.Interrupted);
		result.IsSuccess.ShouldBeFalse();
		result.IsTenureRefused.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].MessageId.ShouldBe(rows[0].Id);
		result.Errors[0].Exception.ShouldBeOfType<InvalidOperationException>();
		result.Errors[0].Error.ShouldContain("999");
		h.Published.ShouldBe(new[] { rows[0].Id });
		h.FailureTokens.ShouldBeEmpty();
		h.AssertNoUnfencedWrites();
		var next = h.Rows("next-operation");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(next);
		h.Complete = (_, _) => ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		var resumed = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		resumed.SuccessCount.ShouldBe(1);
		resumed.IsSuccess.ShouldBeTrue();
		h.Published.ShouldBe(new[] { rows[0].Id, next[0].Id });
		h.FailureTokens.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(OutboxCompletionOutcome.MessageNotFound)]
	public async Task ContinueOtherOwnedRowsAfterKnownRowScopedCompletion(OutboxCompletionOutcome outcome)
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		var rows = h.Rows("first", "second");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Complete = (id, _) => ValueTask.FromResult(id == rows[0].Id ? outcome : OutboxCompletionOutcome.Applied);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.SkippedCount.ShouldBe(0);
		result.Outcome.ShouldBe(PublishingOutcome.Completed);
		h.Published.ShouldBe(rows.Select(row => row.Id));
		h.CompletionTokens.ShouldBe(new long[] { 7, 7 });
		h.FailureTokens.ShouldBeEmpty();
		h.AssertNoUnfencedWrites();
	}
}
