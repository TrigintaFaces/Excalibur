// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Outbox;

namespace Excalibur.Data.Tests.CosmosDb;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbChangeFeedOptionsShould
{
	[Fact]
	public void HaveDefaultLeaseContainerName()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.LeaseContainerName.ShouldBe("outbox-leases");
	}

	[Fact]
	public void HaveDefaultFeedPollInterval()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.FeedPollInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultMaxItemsPerBatch()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.MaxItemsPerBatch.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultInstanceName()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.InstanceName.ShouldBe(Environment.MachineName);
	}

	[Fact]
	public void HaveCreateLeaseContainerEnabledByDefault()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.CreateLeaseContainerIfNotExists.ShouldBeTrue();
	}

	[Fact]
	public void HaveStartFromBeginningDisabledByDefault()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.StartFromBeginning.ShouldBeFalse();
	}
}
