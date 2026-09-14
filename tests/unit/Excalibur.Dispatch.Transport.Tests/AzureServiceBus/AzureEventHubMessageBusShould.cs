// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Proves the CloudEvents mapper is INVOKED on the Event Hubs publish path, not merely accepted by its
/// constructor.
/// </summary>
/// <remarks>
/// <para>
/// The conformance suite checks that a constructor declares a bridge parameter. A bus can satisfy that,
/// store the bridge, log that it resolved it, and ship the native envelope anyway — which is what the Kafka
/// bus did before it was fixed. Constructor shape and mapper invocation differ exactly where that defect
/// lived, so the predicate has to be what reaches the producer.
/// </para>
/// <para>
/// There is already an adapter test that encodes a CloudEvent correctly in isolation. It cannot detect an
/// uninvoked adapter: it never constructs the bus. Only the Event Hubs SDK boundary is faked here — the
/// adapter, bridge and envelope converter are the real production types.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AzureEventHubMessageBusShould
{
	/// <summary>
	/// SAFETY. A bus that accepts the bridge and never calls it fails here.
	/// </summary>
	[Fact]
	public async Task PublishEvent_WhenCloudEventsConfigured_SendsTheMappedCloudEvent()
	{
		var sent = new List<EventData>();
		var producer = FakeProducer(sent);

		var cloudEventOptions = Microsoft.Extensions.Options.Options.Create(
			new CloudEventOptions { DefaultMode = CloudEventMode.Structured });
		var mapper = new AzureEventHubsCloudEventAdapter(cloudEventOptions);
		var bridge = new EnvelopeCloudEventBridge(
			new CloudEventEnvelopeConverter(cloudEventOptions.Value),
			[new CloudEventEncoderAdapter<EventData>(mapper)]);

		var serializer = A.Fake<IPayloadSerializer>();

		await using var bus = new AzureEventHubMessageBus(
			producer,
			serializer,
			NullLogger<AzureEventHubMessageBus>.Instance,
			bridge,
			mapper);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());

		await bus.PublishAsync(new TestCloudEventBusEvent(), context, CancellationToken.None);

		// The mapper owns encoding on this path; a bus that fell through to the native envelope would have
		// consulted the serializer instead.
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).MustNotHaveHappened();

		var data = sent.ShouldHaveSingleItem();
		var body = data.EventBody.ToString();
		body.Contains("specversion", StringComparison.Ordinal).ShouldBeTrue(
			"a structured-mode CloudEvent carries its attributes in the body; without them the mapper "
			+ "never ran");
		body.Contains(nameof(TestCloudEventBusEvent), StringComparison.Ordinal).ShouldBeTrue(
			"the CloudEvent type attribute must name the published event");
	}

	/// <summary>
	/// LIVENESS. Without a bridge the bus still publishes, so the arm above cannot be satisfied by a bus
	/// that simply stopped sending.
	/// </summary>
	[Fact]
	public async Task PublishEvent_WhenCloudEventsNotConfigured_StillSendsTheNativeEnvelope()
	{
		var sent = new List<EventData>();
		var producer = FakeProducer(sent);
		var serializer = A.Fake<IPayloadSerializer>();
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).Returns([1, 2, 3]);

		await using var bus = new AzureEventHubMessageBus(
			producer,
			serializer,
			NullLogger<AzureEventHubMessageBus>.Instance);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());

		await bus.PublishAsync(new TestCloudEventBusEvent(), context, CancellationToken.None);

		_ = sent.ShouldHaveSingleItem();
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).MustHaveHappened();
	}

	/// <summary>
	/// Builds a producer whose batch records what was added, so the assertion reads what reached the wire
	/// rather than what the bus intended to send.
	/// </summary>
	/// <remarks>
	/// Substitutes the framework's own send seam rather than the Azure client. A dynamic proxy over a
	/// vendor class holds only while that class stays open to it: a patch-level SDK refresh that seals a
	/// type or resolves a member differently stops the proxy intercepting, the call reaches the live
	/// service, and this test keeps reporting green while isolating nothing. The seam is declared by this
	/// framework, so no dependency bump can quietly take that away.
	/// </remarks>
	private static IEventHubProducer FakeProducer(List<EventData> sent)
	{
		var producer = A.Fake<IEventHubProducer>();
		var batch = EventHubsModelFactory.EventDataBatch(1024 * 1024, sent);

		A.CallTo(() => producer.CreateBatchAsync(A<CancellationToken>._)).Returns(batch);
		A.CallTo(() => producer.SendAsync(A<EventDataBatch>._, A<CancellationToken>._)).Returns(Task.CompletedTask);

		return producer;
	}

	private sealed class TestCloudEventBusEvent : IDispatchEvent
	{
	}
}
