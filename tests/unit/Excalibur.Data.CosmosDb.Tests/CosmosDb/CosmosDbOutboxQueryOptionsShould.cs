// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Outbox;

namespace Excalibur.Data.Tests.CosmosDb;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CosmosDbOutboxQueryOptionsShould
{
	[Fact]
	public void HaveDefaultPartitionKeyPath()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.PartitionKeyPath.ShouldBe("/partitionKey");
	}

	[Fact]
	public void HaveCrossPartitionQueryDisabledByDefault()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.EnableCrossPartitionQuery.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultMaxConcurrency()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.MaxConcurrency.ShouldBe(-1);
	}

	[Fact]
	public void HaveDefaultMaxBufferedItemCount()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.MaxBufferedItemCount.ShouldBe(-1);
	}

	[Fact]
	public void HaveContinuationTokensEnabledByDefault()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.UseContinuationTokens.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultPreferredPageSize()
	{
		var options = new CosmosDbOutboxQueryOptions();

		options.PreferredPageSize.ShouldBe(100);
	}
}
