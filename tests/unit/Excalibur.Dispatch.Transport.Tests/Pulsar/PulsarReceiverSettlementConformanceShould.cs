// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DotPulsar;
using DotPulsar.Abstractions;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Pulsar;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Pulsar;

/// <summary>Exercises retained receipt retry through actual receive and independent local native observations.</summary>
/// <remarks>Does not certify live broker acceptance, redelivery, or receipt expiry.</remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class PulsarReceiverSettlementConformanceShould
{
	[Theory]
	[InlineData(MessageAction.Acknowledge)]
	[InlineData(MessageAction.Reject)]
	[InlineData(MessageAction.Requeue)]
	public async Task SettleHealthyBatchPeerThenRetryTheOriginalNativeFailure(MessageAction action)
	{
		await using var fixture = new Fixture();
		await fixture.BatchAsync(action);
	}

	private sealed class Fixture : TransportReceiverConformanceTests, IAsyncDisposable
	{
		private const string Topic = "persistent://public/default/settlement-kit";
		private readonly IConsumer<byte[]> _consumer = A.Fake<IConsumer<byte[]>>();
		private readonly Queue<IMessage<byte[]>> _available = new();
		private readonly List<MessageId> _issued = [];

		public Fixture() => A.CallTo(() => _consumer.Receive(A<CancellationToken>._))
			.ReturnsLazily(() => new ValueTask<IMessage<byte[]>>(_available.Dequeue()));
		public ValueTask DisposeAsync() => _consumer.DisposeAsync();
		protected override Task<ITransportReceiver> CreateReceiverAsync() => Task.FromResult<ITransportReceiver>(
			new PulsarTransportReceiver(_consumer, Topic, NullLogger<PulsarTransportReceiver>.Instance));
		protected override Task SeedMessagesAsync(ITransportReceiver receiver, IReadOnlyList<TransportReceivedMessage> messages)
		{
			foreach (var message in messages)
			{
				var id = new MessageId(1, (ulong)(42 + _issued.Count), -1, -1, Topic);
				var native = A.Fake<IMessage<byte[]>>();
				A.CallTo(() => native.MessageId).Returns(id);
				A.CallTo(() => native.Value()).Returns(message.Body.ToArray());
				A.CallTo(() => native.Properties).Returns(new Dictionary<string, string>());
				_issued.Add(id);
				_available.Enqueue(native);
			}
			return Task.CompletedTask;
		}

		public Task BatchAsync(MessageAction action) => VerifySettlementBatchRetry(action, MessageAction.Acknowledge,
			(_, message) =>
			{
				AssertReceipt(message, 0);
				Exception cause = new IOException("Native response lost");
				var attempts = 0;
				ValueTask Attempt() => Interlocked.Increment(ref attempts) == 1 ? new ValueTask(Task.FromException(cause)) : ValueTask.CompletedTask;
				if (action == MessageAction.Requeue)
					A.CallTo(() => _consumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>.That.Matches(ids => ids.Count() == 1 && ids.First().Equals(_issued[0])), A<CancellationToken>._))
						.ReturnsLazily(() => Attempt());
				else
					A.CallTo(() => _consumer.Acknowledge(_issued[0], A<CancellationToken>._)).ReturnsLazily(() => Attempt());
				return Task.FromResult(cause);
			}, SettlementExpectation.Refused(TransportRedeliveryExpectation.Unspecified, SettlementRetryability.Unspecified), SettlementExpectation.Completed,
			(_, message) =>
			{
				AssertReceipt(message, 0);
				AssertEffect(0, action, 1);
				SettlementCalls().ShouldBe(1);
				return Task.CompletedTask;
			}, (_, messages) =>
			{
				AssertReceipt(messages[0], 0);
				AssertReceipt(messages[1], 1);
				AssertEffect(0, action, 2);
				AssertEffect(1, MessageAction.Acknowledge, 1);
				SettlementCalls().ShouldBe(3);
				return Task.CompletedTask;
			}, TimeSpan.FromSeconds(5));

		private void AssertReceipt(TransportReceivedMessage message, int index) => message.ProviderData["pulsar.receipt_handle"].ShouldBe(_issued[index].ToString());
		private void AssertEffect(int index, MessageAction action, int count)
		{
			A.CallTo(() => _consumer.Acknowledge(_issued[index], A<CancellationToken>._)).MustHaveHappened(action == MessageAction.Requeue ? 0 : count, Times.Exactly);
			A.CallTo(() => _consumer.RedeliverUnacknowledgedMessages(A<IEnumerable<MessageId>>.That.Matches(ids => ids.Count() == 1 && ids.First().Equals(_issued[index])), A<CancellationToken>._))
				.MustHaveHappened(action == MessageAction.Requeue ? count : 0, Times.Exactly);
		}
		private int SettlementCalls() => Fake.GetCalls(_consumer).Count(call => call.Method.Name is "Acknowledge" or "RedeliverUnacknowledgedMessages");
	}
}
