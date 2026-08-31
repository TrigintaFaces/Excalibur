// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusCloudEventOptionsShould
{
	[Fact]
	public void AllowNullTimeToLive()
	{
		// Arrange & Act
		var options = new AzureServiceBusCloudEventOptions { TimeToLive = null };

		// Assert
		options.TimeToLive.ShouldBeNull();
	}
}
