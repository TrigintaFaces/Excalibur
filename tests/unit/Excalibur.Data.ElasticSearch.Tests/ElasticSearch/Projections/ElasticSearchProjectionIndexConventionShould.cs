// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for <see cref="ElasticSearchProjectionIndexConvention"/>, ensuring the composed
/// index name is always a valid Elasticsearch index name (lowercase).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
[Trait(TraitNames.Feature, TestFeatures.Projections)]
public sealed class ElasticSearchProjectionIndexConventionShould
{
	[Fact]
	public void LowercaseProjectionTypeName()
	{
		var options = new ElasticSearchProjectionStoreOptions { IndexPrefix = "projections" };

		ElasticSearchProjectionIndexConvention.GetIndexName(options, "OrderSummary")
			.ShouldBe("projections-ordersummary");
	}

	[Fact]
	public void LowercaseUppercaseIndexPrefix()
	{
		var options = new ElasticSearchProjectionStoreOptions { IndexPrefix = "CO-Transactions" };

		ElasticSearchProjectionIndexConvention.GetIndexName(options, "Order")
			.ShouldBe("co-transactions-order");
	}

	[Fact]
	public void LowercaseConsumerSuppliedIndexName()
	{
		var options = new ElasticSearchProjectionStoreOptions { IndexPrefix = string.Empty, IndexName = "MyIndex" };

		ElasticSearchProjectionIndexConvention.GetIndexName(options, "Whatever")
			.ShouldBe("myindex");
	}

	[Fact]
	public void LowercaseIndexNameContainingEnvironmentSegment()
	{
		// Regression: an environment-derived segment ("Development") produced
		// "co-transactions-transaction-Development", which Elasticsearch rejects with a 400
		// invalid_index_name_exception ("must be lowercase").
		var options = new ElasticSearchProjectionStoreOptions
		{
			IndexPrefix = "co-transactions",
			IndexName = "transaction-Development",
		};

		var index = ElasticSearchProjectionIndexConvention.GetIndexName(options, "TransactionProjection");

		index.ShouldBe("co-transactions-transaction-development");
		index.ShouldBe(index.ToLowerInvariant(), "Elasticsearch index names must be lowercase.");
	}
}
