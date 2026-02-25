// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.EventGrid;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class EventGridSchemaModeShould
{
	[Fact]
	public void HaveCloudEventsValue()
	{
		((int)EventGridSchemaMode.CloudEvents).ShouldBe(0);
	}

	[Fact]
	public void HaveEventGridSchemaValue()
	{
		((int)EventGridSchemaMode.EventGridSchema).ShouldBe(1);
	}

	[Fact]
	public void HaveAllMembers()
	{
		Enum.GetValues<EventGridSchemaMode>().Length.ShouldBe(2);
	}
}
