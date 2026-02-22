// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqCloudEventAdapterShould
{
	private readonly RabbitMqCloudEventAdapter _adapter;

	public RabbitMqCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
			DispatchExtensionPrefix = "dispatch",
		});

		_adapter = new RabbitMqCloudEventAdapter(
			options,
			Microsoft.Extensions.Options.Options.Create(new RabbitMqCloudEventOptions()),
			NullLogger<RabbitMqCloudEventAdapter>.Instance);
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqCloudEventAdapter(
				null!,
				Microsoft.Extensions.Options.Options.Create(new RabbitMqCloudEventOptions()),
				NullLogger<RabbitMqCloudEventAdapter>.Instance));
	}

	[Fact]
	public async Task WriteAndRestoreBinaryCloudEventHeaders()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.shipped",
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
		transport.properties.Headers.ShouldContainKey("ce-specversion");
		transport.properties.Headers.ShouldContainKey("ce-id");
		transport.properties.Headers.ShouldContainKey("ce-attempt");

		parsed.Id.ShouldBe("event-1");
		parsed.Type.ShouldBe("orders.shipped");
		parsed["attempt"]?.ToString().ShouldBe("2");
	}

	[Fact]
	public async Task WriteTimeoutHeaderFromTimeoutAttribute()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.shipped",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-2",
			Data = "payload",
		};
		cloudEvent["timeout"] = "2026-02-21T12:00:00Z";

		// Act
		var transport = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Assert
		transport.properties.Headers.ShouldContainKey("ce-timeout");
		transport.properties.Headers["ce-timeout"]?.ToString().ShouldBe("2026-02-21T12:00:00Z");
	}

	[Fact]
	public async Task DetectStructuredModeFromStructuredPayload()
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
		var mode = await RabbitMqCloudEventAdapter.TryDetectMode(transport, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Structured);
	}

	[Fact]
	public async Task DetectBinaryModeFromBinaryPayload()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-4",
			Data = "payload",
		};
		var transport = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);

		// Act
		var mode = await _adapter.TryDetectMode(transport.properties, transport.body, CancellationToken.None);

		// Assert
		mode.ShouldBe(CloudEventMode.Binary);
	}

	[Fact]
	public async Task ReturnInvalidWhenRequiredBinaryHeadersRemoved()
	{
		// Arrange
		var cloudEvent = new CloudEvent(CloudEventsSpecVersion.V1_0)
		{
			Type = "orders.created",
			Source = new Uri("https://source.excalibur.io"),
			Id = "event-5",
			Data = "payload",
		};
		var transport = await _adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Binary, CancellationToken.None);
		transport.properties.Headers!.Remove("ce-id");

		// Act
		var isValid = _adapter.IsValidCloudEventMessage(transport.properties, transport.body);

		// Assert
		isValid.ShouldBeFalse();
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
