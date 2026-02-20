// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait("Category", "Unit")]
public sealed class KafkaMessageBusTransactionsShould : UnitTestBase
{
	[Fact]
	public async Task Publish_WhenTransactionsEnabled_InitializesAndCommitsTransaction()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new KafkaOptions { Topic = "dispatch-topic" };
		var cloudEventOptions = new KafkaCloudEventOptions { EnableTransactions = true };
		var context = A.Fake<IMessageContext>();
		var action = new TestAction();
		var payload = new byte[] { 0x01, 0x02, 0x03 };

		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType()))
				.Returns(payload);
		_ = A.CallTo(() => producer.ProduceAsync(
						A<string>._,
						A<Message<string, byte[]>>._,
						A<CancellationToken>._))
				.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));

		await using var bus = new KafkaMessageBus(
				producer,
				serializer,
				options,
				NullLogger<KafkaMessageBus>.Instance,
				null,
				cloudEventOptions);

		await bus.PublishAsync(action, context, CancellationToken.None);

		_ = A.CallTo(() => producer.InitTransactions(A<TimeSpan>._))
				.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => producer.BeginTransaction())
				.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => producer.CommitTransaction())
				.MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.AbortTransaction())
				.MustNotHaveHappened();
	}

	[Fact]
	public async Task Publish_WhenProduceFails_AbortsTransaction()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new KafkaOptions { Topic = "dispatch-topic" };
		var cloudEventOptions = new KafkaCloudEventOptions { EnableTransactions = true };
		var context = A.Fake<IMessageContext>();
		var action = new TestAction();
		var payload = new byte[] { 0x0A, 0x0B };
		var exception = new KafkaException(new Error(ErrorCode.Local_Transport, "produce failed"));

		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType()))
				.Returns(payload);
		_ = A.CallTo(() => producer.ProduceAsync(
						A<string>._,
						A<Message<string, byte[]>>._,
						A<CancellationToken>._))
				.Returns(Task.FromException<DeliveryResult<string, byte[]>>(exception));

		await using var bus = new KafkaMessageBus(
				producer,
				serializer,
				options,
				NullLogger<KafkaMessageBus>.Instance,
				null,
				cloudEventOptions);

		_ = await Should.ThrowAsync<KafkaException>(
				() => bus.PublishAsync(action, context, CancellationToken.None));

		_ = A.CallTo(() => producer.BeginTransaction())
				.MustHaveHappenedOnceExactly();
		A.CallTo(() => producer.CommitTransaction())
				.MustNotHaveHappened();
		_ = A.CallTo(() => producer.AbortTransaction())
				.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Publish_WhenTransactionsDisabled_SkipsTransactionCalls()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new KafkaOptions { Topic = "dispatch-topic" };
		var cloudEventOptions = new KafkaCloudEventOptions { EnableTransactions = false };
		var context = A.Fake<IMessageContext>();
		var action = new TestAction();
		var payload = new byte[] { 0x0F };

		_ = A.CallTo(() => serializer.SerializeObject(action, action.GetType()))
				.Returns(payload);
		_ = A.CallTo(() => producer.ProduceAsync(
						A<string>._,
						A<Message<string, byte[]>>._,
						A<CancellationToken>._))
				.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));

		await using var bus = new KafkaMessageBus(
				producer,
				serializer,
				options,
				NullLogger<KafkaMessageBus>.Instance,
				null,
				cloudEventOptions);

		await bus.PublishAsync(action, context, CancellationToken.None);

		A.CallTo(() => producer.InitTransactions(A<TimeSpan>._))
				.MustNotHaveHappened();
		A.CallTo(() => producer.BeginTransaction())
				.MustNotHaveHappened();
		A.CallTo(() => producer.CommitTransaction())
				.MustNotHaveHappened();
		A.CallTo(() => producer.AbortTransaction())
				.MustNotHaveHappened();
	}

	private sealed class TestAction : IDispatchAction
	{
	}
}
