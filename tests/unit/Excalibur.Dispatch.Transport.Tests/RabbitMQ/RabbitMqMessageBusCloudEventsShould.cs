// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using Tests.Shared.Categories;

using RabbitMqBasicProperties = RabbitMQ.Client.BasicProperties;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Regression coverage for Excalibur_Dispatch-77mfxz: <c>RabbitMqCloudEventAdapter.ToTransportMessageAsync</c>
/// returned a hand-rolled <c>IBasicProperties</c> implementation that <c>RabbitMqMessageBus</c> could never
/// publish -- <c>IChannel.BasicPublishAsync&lt;TProperties&gt;</c> requires <c>IAmqpHeader</c> too, which
/// <c>IBasicProperties</c> does not extend, so every CloudEvents publish on RabbitMQ threw
/// <see cref="InvalidOperationException"/>. Only <see cref="IChannel"/> is faked here -- the mapper, bridge,
/// and envelope converter are the real production types, matching
/// <c>AwsSqsMessageBusShould.PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventAttributesOnDefaultSend</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMqMessageBusCloudEventsShould : UnitTestBase
{
	[Fact]
	public async Task PublishEvent_WhenCloudEventsConfigured_EmitsCloudEventAttributesOnDefaultSend()
	{
		var channel = A.Fake<IChannel>();
		var serializer = A.Fake<IPayloadSerializer>();
		var options = new RabbitMqOptions { Exchange = "ex", RoutingKey = "rk" };

		var cloudEventOptions = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultMode = CloudEventMode.Binary,
		});
		var mapper = new RabbitMqCloudEventAdapter(
			cloudEventOptions,
			rabbitMqOptions: null,
			NullLogger<RabbitMqCloudEventAdapter>.Instance);
		var bridge = new EnvelopeCloudEventBridge(
			new CloudEventEnvelopeConverter(cloudEventOptions.Value),
			[new CloudEventEncoderAdapter<(IBasicProperties properties, ReadOnlyMemory<byte> body)>(mapper)]);

		RabbitMqBasicProperties? captured = null;
		_ = A.CallTo(() => channel.GetNextPublishSequenceNumberAsync(A<CancellationToken>._))
			.Returns(new ValueTask<ulong>(0));
		_ = A.CallTo(() => channel.BasicPublishAsync(
				A<string>._,
				A<string>._,
				A<bool>._,
				A<RabbitMqBasicProperties>._,
				A<ReadOnlyMemory<byte>>._,
				A<CancellationToken>._))
			.Invokes((string _, string _, bool _, RabbitMqBasicProperties props, ReadOnlyMemory<byte> _, CancellationToken _) => captured = props)
			.Returns(ValueTask.CompletedTask);

		var bus = new RabbitMqMessageBus(
			channel,
			serializer,
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<RabbitMqMessageBus>.Instance,
			cloudEventBridge: bridge,
			cloudEventEncoder: mapper,
			cloudEventOptions: null,
			topologyInitializer: null);

		var evt = new TestCloudEventBusEvent();
		var context = new MessageContext(evt, CreateServiceProvider());

		await bus.PublishAsync(evt, context, CancellationToken.None);

		// The serializer is never consulted on the CloudEvents path -- the mapper owns encoding.
		A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._)).MustNotHaveHappened();

		_ = captured.ShouldNotBeNull();
		captured!.Headers.ShouldNotBeNull();
		captured.Headers.ShouldContainKey("ce-type");
		captured.Headers.ShouldContainKey("ce-id");
		captured.Headers.ShouldContainKey("ce-specversion");
	}

	private static IServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		return services.BuildServiceProvider();
	}

	private sealed class TestCloudEventBusEvent : IDispatchEvent
	{
	}
}
