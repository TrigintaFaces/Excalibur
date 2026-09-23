// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using System.Text.Json;

using Azure.Messaging.EventHubs;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AzureEventHubsCloudEventAdapterShould
{
	private readonly AzureEventHubsCloudEventAdapter _adapter;

	public AzureEventHubsCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});

		var eventHubOptions = Microsoft.Extensions.Options.Options.Create(new AzureEventHubsCloudEventOptions
		{
			UsePartitionKeys = false,
			PartitionKeyStrategy = PartitionKeyStrategy.CorrelationId,
		});

		_adapter = new AzureEventHubsCloudEventAdapter(options, eventHubOptions);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new AzureEventHubsCloudEventAdapter(null!));
	}

	[Fact]
	public async Task ConvertBinaryAndRestoreExtensionAttributes()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.completed",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-1",
			Data = "payload",
			DataContentType = "text/plain",
		};
		cloudEvent["attempt"] = "2";

		// Act
		var transport = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);
		var parsed = await _adapter.FromTransportMessageAsync(transport, CancellationToken.None);

		// Assert
		transport.Properties.ShouldContainKey("cloudEvents_specversion");
		transport.Properties.ShouldContainKey("cloudEvents_type");
		transport.Properties.ShouldContainKey("cloudEvents_source");
		transport.Properties.ShouldContainKey("cloudEvents_id");
		transport.Properties.ShouldContainKey("cloudEvents_attempt");

		parsed.Type.ShouldBe("orders.completed");
		parsed.Id.ShouldBe("event-1");
		parsed["attempt"]?.ToString().ShouldBe("2");
	}

	[Fact]
	public async Task WritePartitionKeyPropertyWhenEnabled()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});
		var eventHubOptions = Microsoft.Extensions.Options.Options.Create(new AzureEventHubsCloudEventOptions
		{
			UsePartitionKeys = true,
			PartitionKeyStrategy = PartitionKeyStrategy.CorrelationId,
		});
		var adapter = new AzureEventHubsCloudEventAdapter(options, eventHubOptions);

		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.completed",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-2",
			Data = "payload",
		};
		cloudEvent["correlationid"] = "corr-42";

		// Act
		var transport = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Assert
		transport.Properties.ShouldContainKey("dispatch-partitionkey");
		transport.Properties["dispatch-partitionkey"]?.ToString().ShouldBe("corr-42");
	}

	[Fact]
	public async Task DetectStructuredModeFromContentType()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-3",
			Data = new { orderId = "A-1" },
			DataContentType = "application/json",
		};
		var transport = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Act
		var mode = await AzureEventHubsCloudEventAdapter.TryDetectMode(transport, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task ThrowJsonReaderExceptionWhenMissingBinaryPropertiesAndBodyIsNotJson()
	{
		// Arrange
		var transport = new EventData(BinaryData.FromString("payload"))
		{
			ContentType = "text/plain",
		};
		// Deliberately the LEGACY "ce-" spelling: this adapter now EMITS "cloudEvents_" per the AMQP
		// binding, but must still READ what earlier versions put on consumers' queues. Changing these
		// three keys to the new prefix would delete the only coverage of that compatibility.
		transport.Properties["ce-specversion"] = "1.0";
		transport.Properties["ce-type"] = "orders.created";
		transport.Properties["ce-source"] = "https://source.excalibur.io";
		// missing ce-id

		// Act / Assert
		await Should.ThrowAsync<JsonException>(() =>
			_adapter.FromTransportMessageAsync(transport, CancellationToken.None));
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
