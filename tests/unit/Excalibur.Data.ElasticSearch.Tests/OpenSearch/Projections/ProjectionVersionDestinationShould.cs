// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearchProjectionGenerations
{
	/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
	public sealed class UnversionedCustomerView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The same read model, labelled as the generation it already is.</summary>
	[ProjectionVersion(1)]
	public sealed class FirstGenerationCustomerView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The next generation of that read model.</summary>
	[ProjectionVersion(2)]
	public sealed class SecondGenerationCustomerView
	{
		/// <summary>Gets or sets the document id.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.ElasticSearch.Tests.OpenSearch.Projections
{
	using FirstGenerationView = Tests.OpenSearchProjectionGenerations.FirstGenerationCustomerView;
	using SecondGenerationView = Tests.OpenSearchProjectionGenerations.SecondGenerationCustomerView;
	using UnversionedView = Tests.OpenSearchProjectionGenerations.UnversionedCustomerView;

	/// <summary>
	/// Binds a declared projection generation to the OpenSearch index a store addresses, and to the
	/// destination identity it advertises.
	/// </summary>
	/// <remarks>
	/// The index is resolved once in the constructor and read by every statement, the index creation and
	/// the identity, so asserting the identity asserts the index: they are one variable, not two
	/// derivations that could disagree. The generation segment is applied AFTER the resolved name is
	/// lowercased, and is digits, so the result stays a legal lowercase index name.
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionVersionDestinationShould
	{
		private const string Node = "http://localhost:9200";

		// The identity an OpenSearch projection resolves to with the shipped default index prefix, no
		// index-name override and no declared generation. HARD-CODED ON PURPOSE: deriving it through
		// GetIndexName or ProjectionVersionConvention.Qualify -- the very seam under test -- would make
		// this arm agree with whatever that seam currently does, which is precisely the drift it exists
		// to catch.
		private const string UnversionedDefaultIdentity = "opensearch/projections-unversionedcustomerview";

		/// <summary>
		/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
		/// destination it addresses today. Without it, a design that versions everything satisfies every
		/// safety arm below while silently re-folding every existing read model from zero.
		/// </summary>
		[Fact]
		public async Task ResolveTheUnchangedIndexAndIdentityForAProjectionDeclaringNoGeneration()
		{
			(await ResolveIdentityAsync<UnversionedView>(options => options.NodeUri = Node))
				.ShouldBe(UnversionedDefaultIdentity);

			(await ResolveIdentityAsync<UnversionedView>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = "readmodels";
				options.IndexName = "customers";
			})).ShouldBe("opensearch/readmodels-customers");

			// The first generation IS the unversioned destination, so an existing read model can adopt
			// the declaration without moving.
			(await ResolveIdentityAsync<FirstGenerationView>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = "readmodels";
				options.IndexName = "customers";
			})).ShouldBe("opensearch/readmodels-customers");
		}

		/// <summary>
		/// SAFETY. A bump moves the destination: a new index, and therefore a new identity. The
		/// configured prefix is untouched -- a generation moves the index inside it.
		/// </summary>
		[Fact]
		public async Task ResolveADifferentIndexAndIdentityWhenTheDeclaredGenerationIsBumped()
		{
			(await ResolveIdentityAsync<SecondGenerationView>(options => options.NodeUri = Node))
				.ShouldBe("opensearch/projections-secondgenerationcustomerview-v2");

			(await ResolveIdentityAsync<SecondGenerationView>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = "readmodels";
				options.IndexName = "customers";
			})).ShouldBe("opensearch/readmodels-customers-v2");
		}

		/// <summary>
		/// SAFETY. Two generations configured onto one index name are two destinations, so the previous
		/// generation keeps its documents and keeps serving reads while the next folds.
		/// </summary>
		[Fact]
		public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddOpenSearchProjectionStore<FirstGenerationView>(
				(Action<OpenSearchProjectionStoreOptions>)(options =>
				{
					options.NodeUri = Node;
					options.IndexPrefix = "readmodels";
					options.IndexName = "customers";
				}));
			_ = services.AddOpenSearchProjectionStore<SecondGenerationView>(
				(Action<OpenSearchProjectionStoreOptions>)(options =>
				{
					options.NodeUri = Node;
					options.IndexPrefix = "readmodels";
					options.IndexName = "customers";
				}));

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var first = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<FirstGenerationView>>());
			var second = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<SecondGenerationView>>());

			first.ShouldNotBe(second, "two generations of one read model must be two destinations");
			first.ShouldBe("opensearch/readmodels-customers");
			second.ShouldBe("opensearch/readmodels-customers-v2");
		}

		/// <summary>
		/// SAFETY. Naming your own index is not an opt-out of generations: the segment is applied to the
		/// fully resolved name, so a configured prefix and index are versioned the same way a default
		/// type name is. The expectation is lowercase because the generation is appended after the
		/// resolved name is lowercased.
		/// </summary>
		[Theory]
		[InlineData("", "Orders", "orders-v2")]
		[InlineData("Tenant-A", "Orders", "tenant-a-orders-v2")]
		public async Task ApplyTheGenerationToAConfiguredIndexNameAndPrefix(
			string prefix, string indexName, string expectedIndex)
		{
			(await ResolveIdentityAsync<SecondGenerationView>(options =>
			{
				options.NodeUri = Node;
				options.IndexPrefix = prefix;
				options.IndexName = indexName;
			})).ShouldBe("opensearch/" + expectedIndex);
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
