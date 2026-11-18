// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class AwsPersistedOutboxObservationShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ObserveOnlyTheActualAcquiredClaimWhoseRealCompletionWasApplied(bool fenced)
	{
		await using var provider = Provider();
		var inner = provider.GetRequiredService<IOutboxStore>();
		var observed = new AwsPersistedOutboxObservation(inner);
		observed.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeSameAs(observed);
		observed.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeSameAs(observed);
		observed.GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeSameAs(observed);
		observed.GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeSameAs(observed);
		observed.GetService(typeof(IOutboxStoreAdmin)).ShouldBeSameAs(inner.GetService(typeof(IOutboxStoreAdmin)));
		var row = new OutboundMessage("observer-control", [1, 2, 3], "local");
		await observed.StageMessageAsync(row, TestContext.Current.CancellationToken);
		var claimed = (fenced
			? await observed.GetUnsentMessagesAsync(1, 7, TestContext.Current.CancellationToken)
			: await observed.GetUnsentMessagesAsync(1, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
		var result = fenced
			? await observed.MarkSentAsync(row.Id, new OutboxWriteAuthority(7, claimed.DispatcherId!), TestContext.Current.CancellationToken)
			: await observed.MarkSentAsync(row.Id, claimed.DispatcherId!, TestContext.Current.CancellationToken);
		result.ShouldBe(OutboxCompletionOutcome.Applied);
		observed.HasTerminal(row.Id).ShouldBeTrue();
		(await ((IOutboxStoreAdmin)inner.GetService(typeof(IOutboxStoreAdmin))!).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken)).SentMessageCount.ShouldBe(1);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverRecordARealRefusalForAnotherClaimAsAWinningTransition(bool fenced)
	{
		await using var provider = Provider();
		var inner = provider.GetRequiredService<IOutboxStore>();
		var observed = new AwsPersistedOutboxObservation(inner);
		var row = new OutboundMessage("observer-refusal", [1], "local");
		await observed.StageMessageAsync(row, TestContext.Current.CancellationToken);
		_ = (fenced ? await observed.GetUnsentMessagesAsync(1, 7, TestContext.Current.CancellationToken)
			: await observed.GetUnsentMessagesAsync(1, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
		var outcome = fenced
			? await observed.MarkSentAsync(row.Id, new OutboxWriteAuthority(7, "not-the-acquired-claimIdentity"), TestContext.Current.CancellationToken)
			: await observed.MarkSentAsync(row.Id, "not-the-acquired-claimIdentity", TestContext.Current.CancellationToken);
		outcome.ShouldBe(OutboxCompletionOutcome.ClaimLost);
		observed.HasTerminal(row.Id).ShouldBeFalse();
		(await ((IOutboxStoreAdmin)inner.GetService(typeof(IOutboxStoreAdmin))!).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken)).StagedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task PropagateTheInnerWriteFaultWithoutRecordingSuccess()
	{
		await using var provider = Provider();
		var fault = new IOException("controlled-observation-write-fault");
		var observed = new AwsPersistedOutboxObservation(new SyntheticSentFacet(provider.GetRequiredService<IOutboxStore>(), fault));
		var row = new OutboundMessage("observer-fault", [1], "local");
		await observed.StageMessageAsync(row, TestContext.Current.CancellationToken);
		var claimIdentity = (await observed.GetUnsentMessagesAsync(1, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
		(await Should.ThrowAsync<IOException>(() => observed.MarkSentAsync(row.Id, claimIdentity.DispatcherId!, TestContext.Current.CancellationToken).AsTask())).ShouldBeSameAs(fault);
		observed.HasTerminal(row.Id).ShouldBeFalse();
	}

	[Fact]
	public async Task NeverTurnSyntheticAppliedForAnUnobservedClaimIntoStateEvidence()
	{
		await using var provider = Provider();
		var inner = provider.GetRequiredService<IOutboxStore>();
		var observed = new AwsPersistedOutboxObservation(new SyntheticSentFacet(inner, null));
		var row = new OutboundMessage("observer-wrong-applied", [1], "local");
		await observed.StageMessageAsync(row, TestContext.Current.CancellationToken);
		_ = (await observed.GetUnsentMessagesAsync(1, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
		(await observed.MarkSentAsync(row.Id, "unobserved-claimIdentity", TestContext.Current.CancellationToken)).ShouldBe(OutboxCompletionOutcome.Applied);
		observed.HasTerminal(row.Id).ShouldBeFalse();
		(await ((IOutboxStoreAdmin)inner.GetService(typeof(IOutboxStoreAdmin))!).GetAllTenantsStatisticsAsync(TestContext.Current.CancellationToken)).StagedMessageCount.ShouldBe(1);
	}

	private static ServiceProvider Provider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch();
		services.AddExcalibur(builder => builder.AddOutbox(outbox => outbox.UseInMemory()));
		return services.BuildServiceProvider();
	}
	private sealed class SyntheticSentFacet(IOutboxStore inner, Exception? fault) : OutboxStoreDecorator(inner), IClaimScopedOutboxStore
	{
		public ValueTask<OutboxCompletionOutcome> MarkSentAsync(string messageId, string claimIdentity, CancellationToken cancellationToken) => fault is null
			? ValueTask.FromResult(OutboxCompletionOutcome.Applied) : ValueTask.FromException<OutboxCompletionOutcome>(fault);
		public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(string messageId, string errorMessage, int retryCount, string claimIdentity, CancellationToken cancellationToken) =>
			((IClaimScopedOutboxStore)Inner.GetService(typeof(IClaimScopedOutboxStore))!).MarkFailedAsync(messageId, errorMessage, retryCount, claimIdentity, cancellationToken);
		public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt, string claimIdentity, CancellationToken cancellationToken) =>
			((IClaimScopedOutboxStore)Inner.GetService(typeof(IClaimScopedOutboxStore))!).MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, claimIdentity, cancellationToken);
	}
}
