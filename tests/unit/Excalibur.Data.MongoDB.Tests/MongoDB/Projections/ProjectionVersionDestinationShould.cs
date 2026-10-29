// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.MongoDB.Tests.MongoProjectionGenerations
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

namespace Excalibur.Data.MongoDB.Tests.MongoDB.Projections
{
	using FirstGenerationView = Tests.MongoProjectionGenerations.FirstGenerationCustomerView;
	using SecondGenerationView = Tests.MongoProjectionGenerations.SecondGenerationCustomerView;
	using UnversionedView = Tests.MongoProjectionGenerations.UnversionedCustomerView;

	/// <summary>
	/// Binds a declared projection generation to the MongoDB collection a store addresses, and to the
	/// destination identity it advertises.
	/// </summary>
	/// <remarks>
	/// The collection is resolved once in the constructor and read by every statement, the index creation
	/// and the identity, so asserting the identity asserts the collection: they are one variable, not two
	/// derivations that could disagree. The DATABASE is deliberately not versioned -- a generation moves
	/// the collection inside it.
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionVersionDestinationShould
	{
		// The identity a MongoDB projection resolves to with the shipped default database and collection
		// and no declared generation. HARD-CODED ON PURPOSE: deriving it through the convention under
		// test would make the arm agree with whatever that convention currently does, which is precisely
		// the drift it exists to catch.
		private const string UnversionedDefaultIdentity = "mongodb/excalibur/projections/UnversionedCustomerView";

		/// <summary>
		/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
		/// destination it addresses today. Without it, a design that versions everything satisfies every
		/// safety arm below while silently re-folding every existing read model from zero.
		/// </summary>
		[Fact]
		public async Task ResolveTheUnchangedCollectionAndIdentityForAProjectionDeclaringNoGeneration()
		{
			(await ResolveIdentityAsync<UnversionedView>(_ => { })).ShouldBe(UnversionedDefaultIdentity);

			(await ResolveIdentityAsync<UnversionedView>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "customers";
			})).ShouldBe("mongodb/people/customers/UnversionedCustomerView");

			// The first generation IS the unversioned destination, so an existing read model can adopt
			// the declaration without moving.
			(await ResolveIdentityAsync<FirstGenerationView>(_ => { })).ShouldBe("mongodb/excalibur/projections/FirstGenerationCustomerView");
		}

		/// <summary>
		/// SAFETY. A bump moves the destination: a new collection, and therefore a new identity. The
		/// database segment is untouched.
		/// </summary>
		[Fact]
		public async Task ResolveADifferentCollectionAndIdentityWhenTheDeclaredGenerationIsBumped()
		{
			(await ResolveIdentityAsync<SecondGenerationView>(_ => { }))
				.ShouldBe("mongodb/excalibur/projections-v2/SecondGenerationCustomerView");

			(await ResolveIdentityAsync<SecondGenerationView>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "customers";
			})).ShouldBe("mongodb/people/customers-v2/SecondGenerationCustomerView");
		}

		/// <summary>
		/// SAFETY. Two generations configured onto one collection name are two destinations, so the
		/// previous generation keeps its documents and keeps serving reads while the next folds.
		/// </summary>
		[Fact]
		public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
		{
			var first = await ResolveIdentityAsync<FirstGenerationView>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "customers";
			});
			var second = await ResolveIdentityAsync<SecondGenerationView>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "customers";
			});

			first.ShouldNotBe(second);
			first.ShouldBe("mongodb/people/customers/FirstGenerationCustomerView");
			second.ShouldBe("mongodb/people/customers-v2/SecondGenerationCustomerView");
		}

		/// <summary>
		/// The convention a consumer repository calls to address the same collection resolves the SAME
		/// generation the store does. Two derivations of one destination is the drift this prevents.
		/// </summary>
		[Fact]
		public async Task ResolveTheSameGeneratedCollectionThroughTheConventionAConsumerRepositoryCalls()
		{
			var options = new MongoDbProjectionStoreOptions { DatabaseName = "people", CollectionName = "customers" };

			MongoDbProjectionCollectionConvention.GetCollectionName<UnversionedView>(options).ShouldBe("customers");
			MongoDbProjectionCollectionConvention.GetCollectionName<FirstGenerationView>(options).ShouldBe("customers");
			MongoDbProjectionCollectionConvention.GetCollectionName<SecondGenerationView>(options).ShouldBe("customers-v2");

			// And that is the collection the store itself addresses, not merely what the helper returns.
			var storeIdentity = await ResolveIdentityAsync<SecondGenerationView>(o =>
			{
				o.DatabaseName = "people";
				o.CollectionName = "customers";
			});
			storeIdentity.ShouldBe("mongodb/people/" +
				MongoDbProjectionCollectionConvention.GetCollectionName<SecondGenerationView>(options) + "/SecondGenerationCustomerView");
		}

		// Resolves through REAL DI -- the provider's own registration extension, a built provider and a
		// scope -- so this binds what a consumer can actually reach, not a hand-constructed store.
		private static async Task<string> ResolveIdentityAsync<TProjection>(
			Action<MongoDbProjectionStoreOptions> configureOptions)
			where TProjection : class
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddMongoDbProjectionStore<TProjection>(configureOptions);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

			// The capability must come from the store's own explicit GetService, never from the default
			// interface implementation on IProjectionStore<TProjection>, which answers
			// serviceType.IsInstanceOfType(this) ? this : null.
			(identity is IProjectionStore<TProjection>).ShouldBeFalse();

			return identity.Identity;
		}
	}
}
