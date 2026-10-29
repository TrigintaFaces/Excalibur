// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.MongoDB.Tests.MongoProjectionIdentity.Sales
{
	/// <summary>A projection type whose simple name collides with its Support sibling.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.MongoDB.Tests.MongoProjectionIdentity.Support
{
	/// <summary>A projection type whose simple name collides with its Sales sibling.</summary>
	public sealed class EmployeeProjection
	{
		/// <summary>Gets or sets the identifier.</summary>
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.MongoDB.Tests.MongoDB.Projections
{
	using SalesProjection = Tests.MongoProjectionIdentity.Sales.EmployeeProjection;
	using SupportProjection = Tests.MongoProjectionIdentity.Support.EmployeeProjection;

	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionDestinationIdentityShould
	{
		// SAFETY. The database segment is the whole reason this arm exists: "employees" repeats across
		// databases, so an identity built from the collection alone collapses two distinct logical
		// targets into one. An implementation emitting only the collection passes a naive equality test
		// and fails here.
		[Fact]
		public async Task QualifyTheCollectionWithItsDatabaseWhenTwoSameNamedProjectionsShareACollectionName()
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			var sales = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.DatabaseName = "sales";
				options.CollectionName = "employees";
			});
			var support = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.DatabaseName = "support";
				options.CollectionName = "employees";
			});

			sales.ShouldBe("mongodb/sales/employees/EmployeeProjection");
			support.ShouldBe("mongodb/support/employees/EmployeeProjection");
			sales.ShouldNotBe(support, "the same collection name in two databases is two logical targets");
		}

		// SAFETY. Differing collections, and differing both, must also separate.
		[Theory]
		[InlineData("people", "employees", "people", "employees_v2")]
		[InlineData("sales", "employees", "support", "staff")]
		public async Task SeparateProjectionsThatAddressDifferentDatabaseAndCollectionPairs(
			string salesDatabase, string salesCollection, string supportDatabase, string supportCollection)
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			var sales = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.DatabaseName = salesDatabase;
				options.CollectionName = salesCollection;
			});
			var support = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.DatabaseName = supportDatabase;
				options.CollectionName = supportCollection;
			});

			sales.ShouldBe("mongodb/" + salesDatabase + "/" + salesCollection + "/EmployeeProjection");
			support.ShouldBe("mongodb/" + supportDatabase + "/" + supportCollection + "/EmployeeProjection");
			sales.ShouldNotBe(support);
		}

		// LIVENESS. Without this arm an implementation that bakes the host, replica set or TLS flag into
		// the identity passes every safety arm above and then refuses every binding made from another
		// environment.
		[Fact]
		public async Task ExcludeConnectionTopologyAndTransportSecurityFromTheLogicalDestinationIdentity()
		{
			var development = await ResolveIdentityAsync<SalesProjection>(options =>
			{
				options.ConnectionString = "mongodb://localhost:27017";
				options.DatabaseName = "people";
				options.CollectionName = "employees";
				options.UseSsl = false;
			});
			var production = await ResolveIdentityAsync<SupportProjection>(options =>
			{
				options.ConnectionString = "mongodb://a.example:27017,b.example:27017,c.example:27017/?replicaSet=rs0";
				options.DatabaseName = "people";
				options.CollectionName = "employees";
				options.UseSsl = true;
			});

			development.ShouldBe(
				production,
				"the identity names the LOGICAL target; the consumer's subscription scope distinguishes clusters, not connection strings");
		}

		// A blank database or collection would emit "mongodb//" and the positioned-projection write
		// requirement rejects a blank identity at startup. It never gets that far: options validation
		// refuses the blank first, loudly.
		[Theory]
		[InlineData("", "employees", "DatabaseName")]
		[InlineData("people", "   ", "CollectionName")]
		public void RefuseABlankDatabaseOrCollectionRatherThanEmitAnEmptyIdentitySegment(
			string databaseName, string collectionName, string expectedMemberInMessage)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddMongoDbProjectionStore<SalesProjection>(options =>
			{
				options.DatabaseName = databaseName;
				options.CollectionName = collectionName;
			});

			using var provider = services.BuildServiceProvider();
			using var scope = provider.CreateScope();

			var thrown = Should.Throw<OptionsValidationException>(
				() => scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>());
			thrown.Message.ShouldContain(expectedMemberInMessage);
		}

		// THE PROOF OF PER-PROJECTION OPTIONS. Both projections are registered in ONE container with
		// different collections -- the shape that previously could not express two destinations at all,
		// because AddMongoDbProjectionStore configured the UNNAMED options instance and the last
		// Configure won for both. The registration now names the instance after the projection's full
		// type name, so each override lands in its own bucket.
		//
		// Both arms matter. Collapsing to the LAST-registered collection is the regression this catches;
		// asserting only "they differ" would also be satisfied by a store that resolved neither
		// consumer's override, so each identity is pinned to its own configured value.
		[Fact]
		public async Task SeparateTwoProjectionsRegisteredInOneContainerWithDifferentCollections()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddMongoDbProjectionStore<SalesProjection>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "sales_employees";
			});
			services.AddMongoDbProjectionStore<SupportProjection>(options =>
			{
				options.DatabaseName = "people";
				options.CollectionName = "support_employees";
			});

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var sales = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>());
			var support = IdentityOf(scope.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>());

			sales.ShouldBe("mongodb/people/sales_employees/EmployeeProjection");
			support.ShouldBe("mongodb/people/support_employees/EmployeeProjection");
			sales.ShouldNotBe(support, "two projections in one container must not share one destination");
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
