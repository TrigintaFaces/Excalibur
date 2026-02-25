// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.EventGrid;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class EventGridTransportOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new EventGridTransportOptions();

		// Assert
		options.TopicEndpoint.ShouldBe(string.Empty);
		options.AccessKey.ShouldBeNull();
		options.UseManagedIdentity.ShouldBeFalse();
		options.SchemaMode.ShouldBe(EventGridSchemaMode.CloudEvents);
		options.Destination.ShouldBe("eventgrid-default");
		options.DefaultEventType.ShouldBe("Excalibur.Dispatch.TransportMessage");
		options.DefaultEventSource.ShouldBe("/excalibur/dispatch");
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new EventGridTransportOptions
		{
			TopicEndpoint = "https://mytopic.westus2-1.eventgrid.azure.net/api/events",
			AccessKey = "key123",
			UseManagedIdentity = true,
			SchemaMode = EventGridSchemaMode.EventGridSchema,
			Destination = "custom-dest",
			DefaultEventType = "MyApp.Event",
			DefaultEventSource = "/myapp",
		};

		// Assert
		options.TopicEndpoint.ShouldBe("https://mytopic.westus2-1.eventgrid.azure.net/api/events");
		options.AccessKey.ShouldBe("key123");
		options.UseManagedIdentity.ShouldBeTrue();
		options.SchemaMode.ShouldBe(EventGridSchemaMode.EventGridSchema);
		options.Destination.ShouldBe("custom-dest");
		options.DefaultEventType.ShouldBe("MyApp.Event");
		options.DefaultEventSource.ShouldBe("/myapp");
	}

	[Fact]
	public void EventGridSchemaModeEnumHaveCorrectValues()
	{
		// Assert
		((int)EventGridSchemaMode.CloudEvents).ShouldBe(0);
		((int)EventGridSchemaMode.EventGridSchema).ShouldBe(1);
	}
}
