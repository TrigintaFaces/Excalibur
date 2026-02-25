// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSubscriptionOptionsShould
{
	[Fact]
	public void DefaultStartPositionToEnd()
	{
		// Arrange & Act
		var options = new EventSubscriptionOptions();

		// Assert
		options.StartPosition.ShouldBe(SubscriptionStartPosition.End);
	}

	[Fact]
	public void DefaultStartPositionValueToZero()
	{
		// Arrange & Act
		var options = new EventSubscriptionOptions();

		// Assert
		options.StartPositionValue.ShouldBe(0L);
	}

	[Fact]
	public void DefaultBufferSizeTo100()
	{
		// Arrange & Act
		var options = new EventSubscriptionOptions();

		// Assert
		options.BufferSize.ShouldBe(100);
	}

	[Fact]
	public void DefaultMaxBatchSizeTo50()
	{
		// Arrange & Act
		var options = new EventSubscriptionOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(50);
	}

	[Fact]
	public void DefaultPollingIntervalToOneSecond()
	{
		// Arrange & Act
		var options = new EventSubscriptionOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void AllowSettingStartPosition()
	{
		// Arrange
		var options = new EventSubscriptionOptions();

		// Act
		options.StartPosition = SubscriptionStartPosition.Beginning;

		// Assert
		options.StartPosition.ShouldBe(SubscriptionStartPosition.Beginning);
	}

	[Fact]
	public void AllowSettingBufferSize()
	{
		// Arrange
		var options = new EventSubscriptionOptions();

		// Act
		options.BufferSize = 500;

		// Assert
		options.BufferSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingPollingInterval()
	{
		// Arrange
		var options = new EventSubscriptionOptions();
		var interval = TimeSpan.FromMilliseconds(250);

		// Act
		options.PollingInterval = interval;

		// Assert
		options.PollingInterval.ShouldBe(interval);
	}
}
