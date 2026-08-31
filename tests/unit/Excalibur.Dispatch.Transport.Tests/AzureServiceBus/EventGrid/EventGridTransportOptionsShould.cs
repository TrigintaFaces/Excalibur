// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.EventGrid;

[Trait(TraitNames.Category, TestCategories.Unit)]
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
		options.SchemaMode.ShouldBe(EventGridSchemaMode.CloudEvents);
		options.Destination.ShouldBe("eventgrid-default");
		options.DefaultEventType.ShouldBe("Excalibur.Dispatch.TransportMessage");
		options.DefaultEventSource.ShouldBe("/excalibur/dispatch");
	}

	[Fact]
	public void EventGridSchemaModeEnumHaveCorrectValues()
	{
		// Assert
		((int)EventGridSchemaMode.CloudEvents).ShouldBe(0);
		((int)EventGridSchemaMode.EventGridSchema).ShouldBe(1);
	}
}
