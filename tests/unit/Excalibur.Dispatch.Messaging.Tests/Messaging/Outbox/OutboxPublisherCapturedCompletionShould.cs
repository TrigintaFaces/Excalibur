// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Outbox")]
public sealed class OutboxPublisherCapturedCompletionShould
{
	[Fact]
	public async Task FreezeWholeBatchSuccessEvidenceBeforeAnyTransportCallback()
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		var rows = h.Rows("first", "second");
		var expected = rows.Select(m => (m.Id, Claim: m.DispatcherId!)).ToArray();
		var reported = new List<(string, string)>();
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>(rows);
		h.Send = _ =>
		{
			foreach (var row in rows) { row.Id = "changed-" + row.Id; row.DispatcherId = "new-claim"; }
			return Task.FromResult<IMessageResult>(MessageResult.Success());
		};
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)h.Store).MarkSentAsync(
			A<string>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily((string id, OutboxWriteAuthority authority, CancellationToken _) =>
			{
				authority.FencingToken.ShouldBe(7);
				reported.Add((id, authority.ClaimIdentity));
				return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
			});
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(2);
		reported.ShouldBe(expected);
		h.Published.ShouldBe(expected.Select(x => x.Id));
		h.AssertNoUnfencedWrites();
		A.CallTo(() => ((IFencedOutboxStore)h.Store).MarkSentAsync(A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FreezeFailureEvidenceBeforeTransportCanMutateTheEnvelope()
	{
		var h = new MessageBusOutboxTenureShould.Harness();
		var row = h.Rows("failed")[0];
		var id = row.Id;
		var identity = row.DispatcherId;
		var failure = new IOException("transport failed");
		h.Claim = _ => ValueTask.FromResult<IEnumerable<OutboundMessage>>([row]);
		h.Send = _ =>
		{
			row.Id = "changed-id";
			row.DispatcherId = "new-claim";
			return Task.FromException<IMessageResult>(failure);
		};
		var observed = new List<(string, string)>();
		A.CallTo(() => ((IFencedClaimScopedOutboxStore)h.Store).MarkFailedAsync(A<string>._, A<string>._,
			A<int>._, A<DateTimeOffset?>._, A<OutboxWriteAuthority>._, A<CancellationToken>._))
			.ReturnsLazily((string messageId, string _, int _, DateTimeOffset? _, OutboxWriteAuthority authority, CancellationToken _) =>
			{
				observed.Add((messageId, authority.ClaimIdentity));
				return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
			});
		var result = await h.Publisher.PublishPendingMessagesAsync(CancellationToken.None);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		observed.ShouldBe(new[] { (id, identity!) });
		result.Errors.Single().MessageId.ShouldBe(id);
		result.Errors.Single().Exception.ShouldBeSameAs(failure);
		h.AssertNoUnfencedWrites();
	}

	[Fact]
	public void RefuseUnsupportedCompletionBeforeAnyTraffic()
	{
		var store = A.Fake<IOutboxStore>().WithHonestCapabilities();
		Should.Throw<InvalidOperationException>(() => new MessageBusOutboxPublisher(store,
			OutboxFakeCapabilityExtensions.CreateRegisteredSerializer(), A.Fake<Excalibur.Dispatch.Transport.IMessageBusAdapter>(),
			A.Fake<IServiceProvider>(), NullLogger<MessageBusOutboxPublisher>.Instance)).Message.ShouldContain(nameof(IClaimScopedOutboxStore));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
	}
}

