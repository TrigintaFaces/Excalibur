// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearchProjectionIdentity.Sales
{
	/// <summary>A projection type whose simple name collides with its Support sibling.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearchProjectionIdentity.Support
{
	/// <summary>A projection type whose simple name collides with its Sales sibling.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearch.Projections
{
	using SalesProjection = Tests.OpenSearchProjectionIdentity.Sales.EmployeeProjection;
	using SupportProjection = Tests.OpenSearchProjectionIdentity.Support.EmployeeProjection;

	/// <summary>
	/// Binds the destination identity an OpenSearch projection store advertises to the index it actually
	/// addresses.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The scheme segment is load-bearing and is asserted literally.</b> OpenSearch and ElasticSearch
	/// are different clusters reached by different clients, and an index name repeats freely between
	/// them. An identity sharing one scheme segment would collapse <c>orders</c> on an OpenSearch
	/// cluster and <c>orders</c> on an ElasticSearch one into a single logical destination, so two
	/// independent read models would contend for one position cursor.
	/// </para>
	/// <para>
	/// <b>Why the index is asserted through the identity.</b> The index is resolved once in the
	/// constructor and every statement, the index creation and the identity read that one field, so
	/// pinning the identity pins the index: one derivation, not two that could disagree.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionDestinationIdentityShould
	{
		private const string Node = "http://localhost:9200";

		// SAFETY. Two projections on different indices are two destinations, and each is pinned to the
		// literal identity it must advertise -- asserting only "they differ" would also be satisfied by
		// a store that resolved neither consumer's configured index.
		[Fact]
		public async Task SeparateTwoSameNamedProjectionsThatAddressDifferentIndices()
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			var sales = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexName = "sales-employees";
			});
			var support = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexName = "support-employees";
			});

			sales.ShouldBe("opensearch/projections-sales-employees");
			support.ShouldBe("opensearch/projections-support-employees");
			sales.ShouldNotBe(support, "two projections in one container must not share one destination");
		}

		// SAFETY. The same index name under two prefixes is likewise two destinations.
		[Theory]
		[InlineData("sales", "employees", "support", "employees")]
		[InlineData("readmodels", "orders", "readmodels", "orders-archive")]
		public async Task SeparateProjectionsThatAddressDifferentPrefixAndIndexPairs(
			string salesPrefix, string salesIndex, string supportPrefix, string supportIndex)
		{
			var sales = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = salesPrefix;
				options.IndexName = salesIndex;
			});
			var support = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = supportPrefix;
				options.IndexName = supportIndex;
			});

			sales.ShouldBe("opensearch/" + salesPrefix + "-" + salesIndex);
			support.ShouldBe("opensearch/" + supportPrefix + "-" + supportIndex);
			sales.ShouldNotBe(support);
		}

		// SAFETY, and the arm that makes the scheme segment load-bearing: the OpenSearch identity must
		// name its own scheme, never the ElasticSearch one. A store emitting "elasticsearch/" passes
		// every other arm in this file -- the identities still separate from each other -- and silently
		// merges a read model on an OpenSearch cluster with a same-named one on an ElasticSearch cluster.
		[Fact]
		public async Task NameTheOpenSearchSchemeRatherThanTheElasticSearchOne()
		{
			var identity = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexName = "orders";
			});

			// OpenSearch and ElasticSearch are distinct clusters; a shared scheme segment would make a
			// same-named index on each look like one logical destination.
			identity.ShouldStartWith("opensearch/");
			identity.ShouldNotStartWith("elasticsearch/");
			identity.ShouldBe("opensearch/projections-orders");
		}

		// LIVENESS. Without this arm a store that bakes the node address, port or debug transport flag
		// into the identity passes every safety arm above and then refuses every binding made from
		// another environment.
		[Fact]
		public async Task ExcludeNodeTopologyAndTransportSettingsFromTheLogicalDestinationIdentity()
		{
			var development = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.NodeUri = "http://localhost:9200";
				options.IndexName = "employees";
				options.EnableDebugMode = true;
			});
			var production = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.NodeUri = "https://os.example:9243";
				options.IndexName = "employees";
				options.EnableDebugMode = false;
			});

			development.ShouldBe(
				production,
				"the identity names the LOGICAL target; the consumer's subscription scope distinguishes "
				+ "clusters, not endpoint strings");
			development.ShouldBe("opensearch/projections-employees");
		}

		// LIVENESS. An index name a consumer capitalised still resolves, because OpenSearch refuses an
		// uppercase index name outright -- so the lowercasing is part of the destination, not cosmetic.
		[Fact]
		public async Task LowercaseAConsumerSuppliedPrefixAndIndexNameInBothTheIndexAndTheIdentity()
		{
			var identity = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = "ReadModels";
				options.IndexName = "EmployeeProjection";
			});

			identity.ShouldBe("opensearch/readmodels-employeeprojection");
		}

		// THE PROOF OF PER-PROJECTION OPTIONS. Both projections are registered in ONE container with
		// different indices. Collapsing to the last-registered index is the regression this catches, so
		// each identity is pinned to its own configured value rather than merely asserted to differ.
		[Fact]
		public async Task SeparateTwoProjectionsRegisteredInOneContainerWithDifferentIndices()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddOpenSearchProjectionStore<SalesProjection>(
				(Action<OpenSearchProjectionStoreOptions>)(options =>
				{
					options.NodeUri = Node;
					options.IndexName = "sales-employees";
				}));
			_ = services.AddOpenSearchProjectionStore<SupportProjection>(
				(Action<OpenSearchProjectionStoreOptions>)(options =>
				{
					options.NodeUri = Node;
					options.IndexName = "support-employees";
				}));

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var sales = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>());
			var support = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>());

			sales.ShouldBe("opensearch/projections-sales-employees");
			support.ShouldBe("opensearch/projections-support-employees");
			sales.ShouldNotBe(support);
		}

		// Resolves through REAL DI -- the provider's own registration extension, a built provider and a
		// scope -- so this binds what a consumer can actually reach, not a hand-constructed store.
		private static async Task<string> ResolveIdentityAsync<TProjection>(
			Action<OpenSearchProjectionStoreOptions> configureOptions)
			where TProjection : class
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddOpenSearchProjectionStore<TProjection>(configureOptions);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			return IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>());
		}

		private static string IdentityOf<TProjection>(IProjectionStore<TProjection> store)
			where TProjection : class
		{
			var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

			// The capability must come from the store's own explicit GetService, never from the default
			// interface implementation on IProjectionStore<TProjection>, which answers
			// serviceType.IsInstanceOfType(this) ? this : null.
			(identity is IProjectionStore<TProjection>).ShouldBeFalse();

			return identity.Identity;
		}
	}
}
