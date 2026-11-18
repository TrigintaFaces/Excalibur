// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class PublishingResultTenureShould
{
	[Fact]
	public async Task PreserveKnownAcceptedFanoutWhenAggregateBookkeepingFails()
	{
		var h = new MessageBusOutboxTenureShould.Harness(multi: true);
		var row = h.Rows("a")[0];
		row.IsMultiTransport = true;
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "one"));
		row.TransportDeliveries.Add(new OutboundMessageTransport(row.Id, "two"));
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		var error = new IOException("aggregate bookkeeping failed");
		A.CallTo(() => ((IMultiTransportOutboxStoreAdmin)h.Store).UpdateAggregateStatusAsync(row.Id, A<CancellationToken>._))
			.ThrowsAsync(error);
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.Outcome.ShouldBe(PublishingOutcome.Interrupted);
		result.IsSuccess.ShouldBeFalse();
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		result.Errors.ShouldContain(e => e.MessageId == row.Id && ReferenceEquals(e.Exception, error));
		A.CallTo(() => ((IMultiTransportOutboxStore)h.Store).MarkTransportSentAsync(row.Id, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();
		h.FailureTokens.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false, PublishingOutcome.Interrupted)]
	[InlineData(true, PublishingOutcome.TenureRefused)]
	public async Task PreservePartialAcceptanceAndPropagateInterruptedOrDominantTenureOutcome(
		bool changeTenure, PublishingOutcome expected)
	{
		var result = await MessageBusOutboxTenureShould.RunRegistryFailureAsync(changeTenure);
		result.Outcome.ShouldBe(expected);
		result.IsTenureRefused.ShouldBe(changeTenure);
		result.IsSuccess.ShouldBeFalse();
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public void PreserveExistingFactoryDefaultsAndDistinguishRefusalFromFailedRows()
	{
		PublishingResult.Success(2).IsTenureRefused.ShouldBeFalse();
		PublishingResult.Success(2).IsSuccess.ShouldBeTrue();
		PublishingResult.WithFailures(1, 1, []).IsTenureRefused.ShouldBeFalse();
		PublishingResult.WithFailures(1, 1, []).IsSuccess.ShouldBeFalse();
		var refused = new PublishingResult { Outcome = PublishingOutcome.TenureRefused };
		refused.IsSuccess.ShouldBeFalse();
		refused.SuccessCount.ShouldBe(0);
		refused.FailureCount.ShouldBe(0);
		refused.Errors.ShouldBeEmpty();
		refused.IsTenureRefused.ShouldBeTrue();
		PublishingResult.Success(1).Outcome.ShouldBe(PublishingOutcome.Completed);
		new PublishingResult { SuccessCount = 1, Outcome = PublishingOutcome.Interrupted }.IsSuccess.ShouldBeFalse();
		new PublishingResult { Outcome = (PublishingOutcome)999 }.IsSuccess.ShouldBeFalse();
		refused.SuccessRate.ShouldBe(100); // Row metric, not full-drain operation verdict.
		refused.ToString().ShouldContain("outcome: TenureRefused");
	}
}
