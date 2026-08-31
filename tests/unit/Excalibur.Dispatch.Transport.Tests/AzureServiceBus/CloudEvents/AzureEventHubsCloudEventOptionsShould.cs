// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class AzureEventHubsCloudEventOptionsShould
{
	[Theory]
	[InlineData(PartitionKeyStrategy.CorrelationId, 0)]
	[InlineData(PartitionKeyStrategy.TenantId, 1)]
	[InlineData(PartitionKeyStrategy.UserId, 2)]
	[InlineData(PartitionKeyStrategy.Source, 3)]
	[InlineData(PartitionKeyStrategy.Type, 4)]
	[InlineData(PartitionKeyStrategy.Custom, 5)]
	public void SupportAllPartitionKeyStrategies(PartitionKeyStrategy strategy, int expectedValue)
	{
		// Assert
		((int)strategy).ShouldBe(expectedValue);
	}
}
