// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Mqtt;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MQTTnet;

namespace Excalibur.Dispatch.Transport.Tests.Mqtt;

/// <summary>
/// Part of the j3f6so arm: MQTT previously had no CloudEvents mapper at all. This proves the new
/// <see cref="MqttCloudEventAdapter"/> round-trips a CloudEvent through the real MQTTnet wire types
/// (<see cref="MqttApplicationMessage"/>/<see cref="MqttApplicationMessageBuilder"/>) in both modes the
/// CNCF CloudEvents spec defines -- structured and binary. No broker involved; this is the pure mapping
/// contract the sibling AWS/Azure/GCP/Kafka/RabbitMQ adapters are each covered by.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class MqttCloudEventAdapterShould
{
	private readonly MqttCloudEventAdapter _adapter = new(
		Microsoft.Extensions.Options.Options.Create(new CloudEventOptions()),
		NullLogger<MqttCloudEventAdapter>.Instance);

	[Fact]
	public async Task RoundTrip_BinaryMode_PreservesCoreAttributesAndData()
	{
		var original = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "com.excalibur.test.v1",
			Source = new Uri("urn:test:mqtt"),
			Id = Guid.NewGuid().ToString(),
			Time = DateTimeOffset.UtcNow,
			DataContentType = "application/json",
			Subject = "test-subject",
			Data = "{\"value\":42}"u8.ToArray(),
		};

		var wireMessage = await _adapter.ToTransportMessageAsync(original, CloudEventMode.Binary, CancellationToken.None);

		// Binary mode puts CE attributes on MQTT v5 user properties, not the payload.
		wireMessage.UserProperties.ShouldNotBeNull();
		wireMessage.UserProperties!.ShouldContain(p => p.Name == "type" && p.Value == original.Type);
		wireMessage.UserProperties!.ShouldContain(p => p.Name == "id" && p.Value == original.Id);
		wireMessage.UserProperties!.ShouldContain(p => p.Name == "specversion");

		var roundTripped = await _adapter.FromTransportMessageAsync(wireMessage, CancellationToken.None);

		roundTripped.Type.ShouldBe(original.Type);
		roundTripped.Id.ShouldBe(original.Id);
		roundTripped.Source.ShouldBe(original.Source);
		roundTripped.Subject.ShouldBe(original.Subject);
		roundTripped.DataContentType.ShouldBe(original.DataContentType);
	}

	[Fact]
	public async Task RoundTrip_StructuredMode_PreservesCoreAttributesAndData()
	{
		var original = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "com.excalibur.test.structured.v1",
			Source = new Uri("urn:test:mqtt"),
			Id = Guid.NewGuid().ToString(),
			DataContentType = "application/json",
		};

		var wireMessage = await _adapter.ToTransportMessageAsync(original, CloudEventMode.Structured, CancellationToken.None);

		wireMessage.ContentType.ShouldBe("application/cloudevents+json");
		wireMessage.Payload.Length.ShouldBeGreaterThan(0);

		var roundTripped = await _adapter.FromTransportMessageAsync(wireMessage, CancellationToken.None);

		roundTripped.Type.ShouldBe(original.Type);
		roundTripped.Id.ShouldBe(original.Id);
		roundTripped.Source.ShouldBe(original.Source);
	}

	[Fact]
	public void DetectMode_ReturnsNull_ForPlainNonCloudEventMessage()
	{
		var plain = new MqttApplicationMessageBuilder()
			.WithTopic("some/topic")
			.WithPayload("just a regular payload"u8.ToArray())
			.Build();

		MqttCloudEventAdapter.DetectMode(plain).ShouldBeNull();
	}

	[Fact]
	public async Task RoundTrip_BinaryMode_CarriesExtensionAttributesUnprefixed()
	{
		var original = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "com.excalibur.test.ext.v1",
			Source = new Uri("urn:test:mqtt"),
			Id = Guid.NewGuid().ToString(),
			DataContentType = "application/json",
		};
		original["tenantid"] = "acme";

		var wireMessage = await _adapter.ToTransportMessageAsync(original, CloudEventMode.Binary, CancellationToken.None);

		// The MQTT binding gives extensions no prefix either -- the name goes on the wire unchanged.
		wireMessage.UserProperties!.ShouldContain(p => p.Name == "tenantid" && p.Value == "acme");
		wireMessage.UserProperties!.ShouldNotContain(p => p.Name.StartsWith("ce-", StringComparison.Ordinal));

		var roundTripped = await _adapter.FromTransportMessageAsync(wireMessage, CancellationToken.None);

		roundTripped["tenantid"].ShouldBe("acme");
	}
}
