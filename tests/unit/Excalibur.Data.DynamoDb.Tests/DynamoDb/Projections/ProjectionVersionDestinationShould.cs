// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.DynamoDb.Tests.ProjectionVersioning
{
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
}

namespace Excalibur.Data.DynamoDb.Tests.DynamoDb.Projections
{
	using FirstGenerationOrderView =
		global::Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.FirstGenerationOrderView;
	using SecondGenerationOrderView =
		global::Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.SecondGenerationOrderView;
	using UnversionedOrderView =
		global::Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.UnversionedOrderView;

	/// <summary>
	/// Binds a declared projection generation to the DISCRIMINATOR a DynamoDB projection store addresses
	/// its items by, and to the destination identity it advertises.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The generation lands on the discriminator, NOT the table name, and that is the load-bearing choice
	/// in this single-table design. The discriminator prefixes every partition key, filters every scan and
	/// carries into the identity, all from one value the store resolves once -- so versioning it moves the
	/// destination without asking the consumer to provision a second table first, and leaves no second
	/// place the generation could disagree. The table a read model lives in is the consumer's
	/// infrastructure; the key prefix is this framework's address inside it.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	[Trait("Database", "DynamoDb")]
	public sealed class ProjectionVersionDestinationShould
	{
		private const string Table = "Projections";

		// The discriminator and identity a DynamoDB projection resolves to with this table and no declared
		// generation. HARD-CODED ON PURPOSE: deriving these through the convention under test would make
		// the arm agree with whatever that convention currently does, which is precisely the drift it
		// exists to catch.
		private const string UnversionedDiscriminator =
			"Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.UnversionedOrderView";

		private const string UnversionedIdentity =
			"dynamodb/Projections/Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.UnversionedOrderView";

		/// <summary>
		/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
		/// discriminator and identity it addresses today, and so does one declaring the first. Without it,
		/// a design that versions everything satisfies every safety arm below while silently re-folding
		/// every existing read model from zero under a key prefix nothing has ever written.
		/// </summary>
		[Fact]
		public async Task ResolveTheUnchangedDiscriminatorAndIdentityForAProjectionDeclaringNoGenerationOrTheFirst()
		{
			DynamoDbProjectionTableConvention
				.GetProjectionDiscriminator<UnversionedOrderView>()
				.ShouldBe(UnversionedDiscriminator);

			(await ResolveIdentityAsync<UnversionedOrderView>()).ShouldBe(UnversionedIdentity);

			DynamoDbProjectionTableConvention
				.GetProjectionDiscriminator<FirstGenerationOrderView>()
				.ShouldBe("Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.FirstGenerationOrderView");
			(await ResolveIdentityAsync<FirstGenerationOrderView>()).ShouldBe(
				"dynamodb/Projections/Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.FirstGenerationOrderView");
		}

		/// <summary>
		/// SAFETY. A bump moves the destination: a new discriminator -- and so a new partition-key prefix
		/// and scan filter -- and therefore a new identity. The TABLE is deliberately untouched.
		/// </summary>
		[Fact]
		public async Task ResolveADifferentDiscriminatorAndIdentityWhenTheDeclaredGenerationIsBumped()
		{
			DynamoDbProjectionTableConvention
				.GetProjectionDiscriminator<SecondGenerationOrderView>()
				.ShouldBe("Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.SecondGenerationOrderView-v2");

			var identity = await ResolveIdentityAsync<SecondGenerationOrderView>();
			identity.ShouldBe(
				"dynamodb/Projections/Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.SecondGenerationOrderView-v2");
			identity.ShouldNotBe(
				UnversionedIdentity,
				"a declared bump that resolved the same discriminator would fold the new shape into the "
					+ "items already serving reads");

			// THE TABLE IS NOT VERSIONED. A bump that renamed the table would need the consumer to create
			// one before the read model could fold at all, and would put the generation in two places.
			DynamoDbProjectionTableConvention
				.GetTableName<SecondGenerationOrderView>(new DynamoDbProjectionStoreOptions { TableName = Table })
				.ShouldBe(Table);
			identity.ShouldStartWith("dynamodb/" + Table + "/");

			// THE OPTIONS BUCKET IS NOT VERSIONED either: a bumped read model keeps reading the settings
			// its registration configured rather than falling back to an unconfigured instance.
			DynamoDbProjectionTableConvention
				.GetOptionsName<SecondGenerationOrderView>()
				.ShouldBe("Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.SecondGenerationOrderView");
		}

		/// <summary>
		/// SAFETY. Two generations of one read model, in ONE table, are two destinations resolvable at
		/// once -- so the previous generation keeps its items and keeps serving reads while the next folds.
		/// </summary>
		[Fact]
		public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(A.Fake<IAmazonDynamoDB>());
			services.AddDynamoDbProjectionStore<FirstGenerationOrderView>(options => options.TableName = Table);
			services.AddDynamoDbProjectionStore<SecondGenerationOrderView>(options => options.TableName = Table);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			var first = Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<FirstGenerationOrderView>>());
			var second = Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<SecondGenerationOrderView>>());

			first.ShouldBe(
				"dynamodb/Projections/Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.FirstGenerationOrderView");
			second.ShouldBe(
				"dynamodb/Projections/Excalibur.Data.DynamoDb.Tests.ProjectionVersioning.SecondGenerationOrderView-v2");
			first.ShouldNotBe(second);
		}

		private static async Task<string> ResolveIdentityAsync<TProjection>()
			where TProjection : class
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(A.Fake<IAmazonDynamoDB>());
			services.AddDynamoDbProjectionStore<TProjection>(options => options.TableName = Table);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();

			return Identity(scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>());
		}

		private static string Identity<TProjection>(IProjectionStore<TProjection> store)
			where TProjection : class
		{
			var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

			// The capability must be a distinct object rather than the store answering through the default
			// interface implementation, which hands back `this` for any assignable type. Without this the
			// arm would pass for a store that resolves no identity at all.
			(identity is IProjectionStore<TProjection>).ShouldBeFalse(
				"the identity must come from the store's own GetService, not the default interface implementation");

			return identity.Identity;
		}
	}
}
