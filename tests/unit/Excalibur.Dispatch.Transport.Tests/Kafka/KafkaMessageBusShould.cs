// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Regression coverage for the Kafka arm of 2hgehp. Before this fix, <c>KafkaMessageBus</c> accepted
/// an <c>ICloudEventEncoder</c>, null-checked it, and logged that it had "resolved" the mapper for the
/// publish path -- then never called any of its methods, so the native envelope shipped regardless of
/// configuration. This proves the mapper is now actually INVOKED: only <see cref="IProducer{TKey,TValue}"/>
/// is faked, the mapper/bridge/envelope-converter are the real production types.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KafkaMessageBusShould : UnitTestBase
{
	[Fact]
	public async Task PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventOnDefaultSend()
	{
		var producer = A.Fake<IProducer<string, byte[]>>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new KafkaOptions { Topic = "dispatch-topic" };

		// Binary mode is what 2hgehp's AC literally asks to assert (specversion/type/source/id as
		// wire headers/attributes). Kafka's structured mode carries only a content-type header plus
		// the whole CloudEvent as the JSON body -- it does not duplicate ce_* headers there.
		var cloudEventOptions = Microsoft.Extensions.Options.Options.Create(
			new CloudEventOptions { DefaultMode = CloudEventMode.Binary });
		var mapper = new KafkaCloudEventAdapter(
			cloudEventOptions,
			new KafkaCloudEventOptions(),
			NullLogger<KafkaCloudEventAdapter>.Instance);
		var bridge = new EnvelopeCloudEventBridge(
			new CloudEventEnvelopeConverter(cloudEventOptions.Value),
			[new CloudEventEncoderAdapter<Message<string, string>>(mapper)]);

		Message<string, byte[]>? captured = null;
		_ = A.CallTo(() => producer.ProduceAsync(A<string>._, A<Message<string, byte[]>>._, A<CancellationToken>._))
			.Invokes((string _, Message<string, byte[]> m, CancellationToken _) => captured = m)
			.Returns(Task.FromResult(new DeliveryResult<string, byte[]>()));

		await using var bus = new KafkaMessageBus(
			producer,
			serializer,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<KafkaMessageBus>.Instance,
			mapper,
			cloudEventOptions: null,
			cloudEventBridge: bridge);

		var evt = new TestCloudEventBusEvent();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());

		await bus.PublishAsync(evt, context, CancellationToken.None);

		// The serializer is never consulted on the CloudEvents path -- the mapper owns encoding.
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).MustNotHaveHappened();

		_ = captured.ShouldNotBeNull();
		// Kafka's CloudEvents binding prefixes headers with "ce_" (underscore) -- a Kafka header key
		// cannot carry the HTTP-style "ce-" hyphen -- unlike AWS/RabbitMQ's "ce-" attributes.
		var headerNames = captured!.Headers!.Select(h => h.Key).ToArray();
		headerNames.ShouldContain("ce_type");
		headerNames.ShouldContain("ce_id");
		headerNames.ShouldContain("ce_specversion");
	}

	private sealed class TestCloudEventBusEvent : IDispatchEvent
	{
	}
}
