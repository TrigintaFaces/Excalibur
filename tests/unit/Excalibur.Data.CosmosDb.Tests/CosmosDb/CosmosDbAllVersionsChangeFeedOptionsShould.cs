// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Cdc;

namespace Excalibur.Data.Tests.CosmosDb;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbAllVersionsChangeFeedOptionsShould
{
	[Fact]
	public void HaveDefaultLeaseContainer()
	{
		var options = new CosmosDbAllVersionsChangeFeedOptions();

		options.LeaseContainer.ShouldBe("leases");
	}

	[Fact]
	public void HaveDefaultFeedPollInterval()
	{
		var options = new CosmosDbAllVersionsChangeFeedOptions();

		options.FeedPollInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveNullStartTimeByDefault()
	{
		var options = new CosmosDbAllVersionsChangeFeedOptions();

		options.StartTime.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		var options = new CosmosDbAllVersionsChangeFeedOptions();

		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultProcessorName()
	{
		var options = new CosmosDbAllVersionsChangeFeedOptions();

		options.ProcessorName.ShouldBe("all-versions-cdc-processor");
	}

	[Fact]
	public void AcceptCustomValues()
	{
		var startTime = DateTimeOffset.UtcNow;
		var options = new CosmosDbAllVersionsChangeFeedOptions
		{
			LeaseContainer = "custom-leases",
			FeedPollInterval = TimeSpan.FromSeconds(10),
			StartTime = startTime,
			MaxBatchSize = 500,
			ProcessorName = "my-processor"
		};

		options.LeaseContainer.ShouldBe("custom-leases");
		options.FeedPollInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.StartTime.ShouldBe(startTime);
		options.MaxBatchSize.ShouldBe(500);
		options.ProcessorName.ShouldBe("my-processor");
	}
}
