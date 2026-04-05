// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbChangeFeedOptions"/> class.
/// </summary>
/// <remarks>
/// Sprint 633: Updated for Cdc.CosmosDb extraction -- new API surface.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CosmosDbChangeFeedOptionsShould
{
	[Fact]
	public void HaveDefaultMode()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.Mode.ShouldBe(CosmosDbCdcMode.LatestVersion);
	}

	[Fact]
	public void HaveDefaultStartPosition()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.StartPosition.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultPollInterval()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultMaxWaitTime()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveIncludeTimestampEnabledByDefault()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.IncludeTimestamp.ShouldBeTrue();
	}

	[Fact]
	public void HaveIncludeLsnEnabledByDefault()
	{
		var options = new CosmosDbChangeFeedOptions();

		options.IncludeLsn.ShouldBeTrue();
	}
}
