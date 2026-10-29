// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using SalesProjection = Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Sales.EmployeeProjection;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections;

/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
public sealed class UnversionedOrderView
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The same read model, labelled as the generation it already is.</summary>
[ProjectionVersion(1)]
public sealed class FirstGenerationOrderView
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>The next generation of that read model.</summary>
[ProjectionVersion(2)]
public sealed class SecondGenerationOrderView
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Binds a declared projection generation to the ElasticSearch index a store addresses, and to the
/// destination identity it advertises.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionVersionDestinationShould
{
	// The index and identity an ElasticSearch projection resolves to with default options and no
	// declared generation. HARD-CODED ON PURPOSE: deriving these through the convention under test
	// would make the arm agree with whatever that convention currently does, which is precisely the
	// drift it exists to catch.
	private const string UnversionedIndex = "projections-unversionedorderview";
	private const string UnversionedIdentity = "elasticsearch/projections-unversionedorderview";

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElasticSearchProjectionStore<UnversionedOrderView>(options => options.NodeUri = "http://localhost:9200");
		services.AddElasticSearchProjectionStore<FirstGenerationOrderView>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexName = "orders";
		});
		services.AddElasticSearchProjectionStore<SecondGenerationOrderView>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexName = "orders";
		});
		services.AddElasticSearchProjectionStore<SalesProjection>(options => options.NodeUri = "http://localhost:9200");
		return services.BuildServiceProvider();
	}

	private static IProjectionDestinationIdentity IdentityOf<TProjection>(IServiceProvider scoped)
		where TProjection : class
	{
		var store = scoped.GetRequiredService<IProjectionStore<TProjection>>();
		var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

		// The capability must be a distinct object rather than the store answering through a default
		// interface implementation, or this arm would pass for a store that resolves no identity at all.
		(identity is IProjectionStore<TProjection>).ShouldBeFalse();
		return identity;
	}

	private static ElasticSearchProjectionStoreOptions OptionsFor<TProjection>(IServiceProvider provider)
		where TProjection : class =>
		provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>()
			.Get(ElasticSearchProjectionIndexConvention.GetOptionsName<TProjection>());

	/// <summary>
	/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
	/// destination it addresses today. Without it, a design that versions everything satisfies every
	/// safety arm below while silently re-folding every existing read model from zero.
	/// </summary>
	[Fact]
	public async Task ResolveTheUnchangedIndexAndIdentityForAProjectionDeclaringNoGeneration()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();
		var options = OptionsFor<UnversionedOrderView>(provider);

		ElasticSearchProjectionIndexConvention
			.GetIndexName<UnversionedOrderView>(options)
			.ShouldBe(UnversionedIndex);

		// Through the runtime-Type overload as well, because that is the form the start-up
		// index-collision check calls and it must agree with the generic one. Held in a variable so
		// this reads as the check does -- a Type it was handed, not one known at the call site.
		Type projectionType = typeof(UnversionedOrderView);
		ElasticSearchProjectionIndexConvention
			.GetIndexName(options, projectionType)
			.ShouldBe(UnversionedIndex);

		IdentityOf<UnversionedOrderView>(scope.ServiceProvider).Identity.ShouldBe(UnversionedIdentity);
	}

	/// <summary>
	/// LIVENESS. The first generation is the unversioned destination, so an existing read model can
	/// adopt the declaration without moving.
	/// </summary>
	[Fact]
	public async Task ResolveTheUnchangedIndexAndIdentityForAProjectionDeclaringTheFirstGeneration()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		ElasticSearchProjectionIndexConvention
			.GetIndexName<FirstGenerationOrderView>(OptionsFor<FirstGenerationOrderView>(provider))
			.ShouldBe("projections-orders");
		IdentityOf<FirstGenerationOrderView>(scope.ServiceProvider)
			.Identity.ShouldBe("elasticsearch/projections-orders");
	}

	/// <summary>SAFETY. A bump moves the destination: a new index, and therefore a new identity.</summary>
	[Fact]
	public async Task ResolveADifferentIndexAndIdentityWhenTheDeclaredGenerationIsBumped()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		ElasticSearchProjectionIndexConvention
			.GetIndexName<SecondGenerationOrderView>(OptionsFor<SecondGenerationOrderView>(provider))
			.ShouldBe("projections-orders-v2");
		IdentityOf<SecondGenerationOrderView>(scope.ServiceProvider)
			.Identity.ShouldBe("elasticsearch/projections-orders-v2");
	}

	/// <summary>
	/// SAFETY. Two generations configured onto one index name are two destinations, concurrently
	/// resolvable, so the previous generation keeps serving reads while the next folds.
	/// </summary>
	[Fact]
	public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		var first = IdentityOf<FirstGenerationOrderView>(scope.ServiceProvider);
		var second = IdentityOf<SecondGenerationOrderView>(scope.ServiceProvider);

		first.Identity.ShouldNotBe(second.Identity);
		first.Identity.ShouldBe("elasticsearch/projections-orders");
		second.Identity.ShouldBe("elasticsearch/projections-orders-v2");
	}

	/// <summary>
	/// A configured index name and prefix are versioned the same way the default type name is: naming
	/// your own index is not an opt-out of generations.
	/// </summary>
	[Theory]
	[InlineData("", "orders", "orders-v2")]
	[InlineData("tenant-a", "orders", "tenant-a-orders-v2")]
	public async Task ApplyTheGenerationToAConfiguredIndexNameAndPrefix(string prefix, string indexName, string expected)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElasticSearchProjectionStore<SecondGenerationOrderView>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexPrefix = prefix;
			options.Index.IndexName = indexName;
		});
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();

		IdentityOf<SecondGenerationOrderView>(scope.ServiceProvider).Identity.ShouldBe("elasticsearch/" + expected);
	}

	/// <summary>
	/// The overload taking a type NAME cannot see a declaration that lives on the type, so it resolves
	/// the first generation. Pinned so a provider author does not reach for it expecting otherwise.
	/// </summary>
	[Fact]
	public async Task ResolveTheFirstGenerationThroughTheOverloadTakingATypeName()
	{
		await using var provider = BuildProvider();

		ElasticSearchProjectionIndexConvention
			.GetIndexName(OptionsFor<SecondGenerationOrderView>(provider), nameof(SecondGenerationOrderView))
			.ShouldBe("projections-orders");
	}

	/// <summary>LIVENESS, against a fixture that predates generations entirely.</summary>
	[Fact]
	public async Task LeaveAPreExistingProjectionsDefaultIndexUntouched()
	{
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		IdentityOf<SalesProjection>(scope.ServiceProvider)
			.Identity.ShouldBe("elasticsearch/projections-employeeprojection");
	}
}
