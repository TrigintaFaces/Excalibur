// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using CloudNative.CloudEvents;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsEventBridgeCloudEventAdapterShould
{
	private readonly AwsEventBridgeCloudEventAdapter _adapter;

	public AwsEventBridgeCloudEventAdapterShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
		{
			DefaultSource = new Uri("https://test.excalibur.io"),
			DefaultMode = CloudEventMode.Structured,
		});

		_adapter = new AwsEventBridgeCloudEventAdapter(
			options,
			EventBridgeOptions(),
			NullLogger<AwsEventBridgeCloudEventAdapter>.Instance);
	}

	private static IOptions<AwsEventBridgeCloudEventOptions> EventBridgeOptions(
		AwsEventBridgeCloudEventOptions? value = null) =>
		Microsoft.Extensions.Options.Options.Create(value ?? new AwsEventBridgeCloudEventOptions());

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsEventBridgeCloudEventAdapter(
				null!, EventBridgeOptions(), NullLogger<AwsEventBridgeCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenEventBridgeOptionsIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());

		Should.Throw<ArgumentNullException>(() =>
			new AwsEventBridgeCloudEventAdapter(
				options, null!, NullLogger<AwsEventBridgeCloudEventAdapter>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CloudEventOptions());

		Should.Throw<ArgumentNullException>(() =>
			new AwsEventBridgeCloudEventAdapter(options, EventBridgeOptions(), null!));
	}

	[Fact]
	public async Task HonorConfiguredEventBusName_OnSend()
	{
		var adapter = new AwsEventBridgeCloudEventAdapter(
			Microsoft.Extensions.Options.Options.Create(new CloudEventOptions
			{
				DefaultSource = new Uri("https://test.excalibur.io"),
			}),
			EventBridgeOptions(new AwsEventBridgeCloudEventOptions { EventBusName = "orders-bus" }),
			NullLogger<AwsEventBridgeCloudEventAdapter>.Instance);

		var cloudEvent = new CloudEvent
		{
			Type = "order.created",
			Source = new Uri("https://test.excalibur.io"),
			Id = "evt-1",
		};

		var entry = await adapter.ToTransportMessageAsync(cloudEvent, CloudEventMode.Structured, TestContext.Current.CancellationToken);

		entry.EventBusName.ShouldBe("orders-bus");
	}

	[Fact]
	public void ExposeOptions()
	{
		_adapter.Options.ShouldNotBeNull();
		_adapter.Options.DefaultSource.ShouldNotBeNull();
	}

	[Fact]
	public async Task ConvertCloudEventToEventBridgeEntry()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-1",
			Data = "test data",
			DataContentType = "text/plain",
		};

		// Act
		var entry = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Assert
		entry.ShouldNotBeNull();
		entry.DetailType.ShouldBe("test.event");
		entry.Detail.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task ThrowWhenCloudEventIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _adapter.ToTransportMessageAsync(
				null!, CloudEventMode.Structured, CancellationToken.None));
	}

	[Fact]
	public async Task ConvertToEventBridgeEventWithBusName()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-2",
			Data = "test data",
		};

		// Act
		var entry = await _adapter.ToEventBridgeEventAsync(
			cloudEvent, "my-event-bus", CancellationToken.None);

		// Assert
		entry.EventBusName.ShouldBe("my-event-bus");
	}

	[Fact]
	public async Task ThrowWhenEventBusNameIsEmpty()
	{
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-3",
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _adapter.ToEventBridgeEventAsync(
				cloudEvent, "", CancellationToken.None));
	}

	[Fact]
	public async Task IncludeSubjectInResources()
	{
		// Arrange
		var cloudEvent = new CloudEvent
		{
			Type = "test.event",
			Source = new Uri("https://source.example.com"),
			Id = "test-id-4",
			Subject = "my-subject",
			Data = "test data",
		};

		// Act
		var entry = await _adapter.ToTransportMessageAsync(
			cloudEvent, CloudEventMode.Structured, CancellationToken.None);

		// Assert
		entry.Resources.ShouldContain("my-subject");
	}
}

#pragma warning restore IL2026
#pragma warning restore IL3050
