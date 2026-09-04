// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Ordering;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class OrderingModelsShould
{
	[Fact]
	public void CreateOrderingKeyStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new OrderingKeyStatistics();

		// Assert
		stats.TotalOrderingKeys.ShouldBe(0);
		stats.ActiveOrderingKeys.ShouldBe(0);
		stats.FailedOrderingKeys.ShouldBe(0);
		stats.TotalMessagesProcessed.ShouldBe(0);
		stats.TotalOutOfSequenceMessages.ShouldBe(0);
		stats.TotalProcessed.ShouldBe(0);
		stats.TotalErrors.ShouldBe(0);
		stats.AverageProcessingTime.ShouldBe(0.0);
		stats.AverageQueueDepth.ShouldBe(0.0);
		stats.QueueStatistics.ShouldNotBeNull();
		stats.QueueStatistics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateOrderingKeyStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new OrderingKeyStatistics
		{
			TotalOrderingKeys = 50,
			ActiveOrderingKeys = 45,
			FailedOrderingKeys = 5,
			TotalMessagesProcessed = 100000,
			TotalOutOfSequenceMessages = 150,
			TotalProcessed = 99850,
			TotalErrors = 150,
			AverageProcessingTime = 12.5,
			AverageQueueDepth = 3.2,
		};

		// Assert
		stats.TotalOrderingKeys.ShouldBe(50);
		stats.ActiveOrderingKeys.ShouldBe(45);
		stats.FailedOrderingKeys.ShouldBe(5);
		stats.TotalMessagesProcessed.ShouldBe(100000);
		stats.TotalOutOfSequenceMessages.ShouldBe(150);
		stats.TotalProcessed.ShouldBe(99850);
		stats.TotalErrors.ShouldBe(150);
		stats.AverageProcessingTime.ShouldBe(12.5);
		stats.AverageQueueDepth.ShouldBe(3.2);
	}

}
