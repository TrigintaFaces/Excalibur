// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AzureEnumsShould
{
	[Theory]
	[InlineData(PartitionKeyStrategy.CorrelationId, 0)]
	[InlineData(PartitionKeyStrategy.TenantId, 1)]
	[InlineData(PartitionKeyStrategy.UserId, 2)]
	[InlineData(PartitionKeyStrategy.Source, 3)]
	[InlineData(PartitionKeyStrategy.Type, 4)]
	[InlineData(PartitionKeyStrategy.Custom, 5)]
	public void HaveCorrectPartitionKeyStrategyValues(PartitionKeyStrategy strategy, int expected)
	{
		((int)strategy).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllPartitionKeyStrategyMembers()
	{
		Enum.GetValues<PartitionKeyStrategy>().Length.ShouldBe(6);
	}

	[Theory]
	[InlineData(RetryMode.Fixed, 0)]
	[InlineData(RetryMode.Exponential, 1)]
	public void HaveCorrectRetryModeValues(RetryMode mode, int expected)
	{
		((int)mode).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllRetryModeMembers()
	{
		Enum.GetValues<RetryMode>().Length.ShouldBe(2);
	}

	[Theory]
	[InlineData(EventHubStartingPosition.Earliest, 0)]
	[InlineData(EventHubStartingPosition.Latest, 1)]
	[InlineData(EventHubStartingPosition.FromTimestamp, 2)]
	public void HaveCorrectEventHubStartingPositionValues(EventHubStartingPosition position, int expected)
	{
		((int)position).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllEventHubStartingPositionMembers()
	{
		Enum.GetValues<EventHubStartingPosition>().Length.ShouldBe(3);
	}
}
