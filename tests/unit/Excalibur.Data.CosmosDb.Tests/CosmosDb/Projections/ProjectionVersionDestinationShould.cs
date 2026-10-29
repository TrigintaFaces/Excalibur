// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Data.CosmosDb.Tests.ProjectionVersioning
{
	/// <summary>A read model declaring no generation. Stands in for every projection that exists today.</summary>
	internal sealed class UnversionedOrderView
	{
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The same read model, labelled as the generation it already is.</summary>
	[ProjectionVersion(1)]
	internal sealed class FirstGenerationOrderView
	{
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The next generation of that read model.</summary>
	[ProjectionVersion(2)]
	internal sealed class SecondGenerationOrderView
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.CosmosDb.Tests.CosmosDb.Projections
{
	using Excalibur.Data.CosmosDb.Projections;

	using FirstGenerationOrderView = global::Excalibur.Data.CosmosDb.Tests.ProjectionVersioning.FirstGenerationOrderView;
	using SecondGenerationOrderView = global::Excalibur.Data.CosmosDb.Tests.ProjectionVersioning.SecondGenerationOrderView;
	using UnversionedOrderView = global::Excalibur.Data.CosmosDb.Tests.ProjectionVersioning.UnversionedOrderView;

	/// <summary>
	/// Binds a declared projection generation to the Cosmos CONTAINER a store addresses, and to the
	/// destination identity it advertises.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The generation lands on the container, never the database: the database is provisioned by the
	/// consumer, while the container is the segment this framework creates on first use. A bump therefore
	/// folds into a new container inside the database already configured.
	/// </para>
	/// <para>
	/// No emulator is involved and none is needed. Both the container the writes resolve through and the
	/// identity are computed from bound options at construction, before any request is issued.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	[Trait("Database", "CosmosDb")]
	public sealed class ProjectionVersionDestinationShould
	{
		private const string Endpoint = "https://localhost:8081/";

		// The well-known Cosmos emulator key. Not a credential: it is published by Microsoft, identical on
		// every emulator install, and nothing here opens a connection. pragma: allowlist secret
		private const string EmulatorKey =
			"C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

		private const string Database = "orders-db";

		private const string Container = "order-summaries";

		// The container and identity a Cosmos projection resolves to with this configuration and no
		// declared generation. HARD-CODED ON PURPOSE: deriving these through the convention under test
		// would make the arm agree with whatever that convention currently does, which is precisely the
		// drift it exists to catch.
		private const string UnversionedContainer = "order-summaries";
		private const string UnversionedIdentity = "cosmosdb/orders-db/order-summaries/UnversionedOrderView";

		/// <summary>
		/// LIVENESS, and the arm that matters most: a projection declaring no generation keeps the exact
		/// destination it addresses today, and so does one declaring the first. Without it, a design that
		/// versions everything satisfies every safety arm below while silently re-folding every existing
		/// read model from zero into a container that does not exist yet.
		/// </summary>
		[Fact]
		public async Task ResolveTheUnchangedContainerAndIdentityForAProjectionDeclaringNoGenerationOrTheFirst()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddStore<UnversionedOrderView>(services);
			AddStore<FirstGenerationOrderView>(services);

			await using var host = services.BuildServiceProvider();

			OptionsFor<UnversionedOrderView>(host, out var unversionedOptions);
			CosmosDbProjectionContainerConvention
				.GetContainerName<UnversionedOrderView>(unversionedOptions)
				.ShouldBe(UnversionedContainer);

			// Through the runtime-Type overload as well, because that is the form a caller holding only a
			// Type reaches for and it must agree with the generic one. Held in a variable so this reads as
			// such a caller does -- a Type it was handed, not one known at the call site.
			Type projectionType = typeof(UnversionedOrderView);
			CosmosDbProjectionContainerConvention
				.GetContainerName(unversionedOptions, projectionType)
				.ShouldBe(UnversionedContainer);

			(await ResolveIdentityAsync<UnversionedOrderView>(host)).ShouldBe(UnversionedIdentity);
			(await ResolveIdentityAsync<FirstGenerationOrderView>(host)).ShouldBe("cosmosdb/orders-db/order-summaries/FirstGenerationOrderView");
		}

		/// <summary>SAFETY. A bump moves the destination: a new container, and therefore a new identity.</summary>
		[Fact]
		public async Task ResolveADifferentContainerAndIdentityWhenTheDeclaredGenerationIsBumped()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddStore<SecondGenerationOrderView>(services);

			await using var host = services.BuildServiceProvider();

			OptionsFor<SecondGenerationOrderView>(host, out var options);
			CosmosDbProjectionContainerConvention
				.GetContainerName<SecondGenerationOrderView>(options)
				.ShouldBe("order-summaries-v2");

			var identity = await ResolveIdentityAsync<SecondGenerationOrderView>(host);
			identity.ShouldBe("cosmosdb/orders-db/order-summaries-v2/SecondGenerationOrderView");
			identity.ShouldNotBe(
				UnversionedIdentity,
				"a declared bump that resolved the same container would fold the new shape into the "
					+ "documents already serving reads");

			// The DATABASE is untouched. Versioning it instead would demand the consumer provision a
			// database before a read model could declare a new shape.
			identity.ShouldStartWith("cosmosdb/" + Database + "/");
		}

		/// <summary>
		/// SAFETY. Two generations of one read model, configured onto one container name and registered in
		/// ONE service collection, are two destinations resolvable at once -- so the previous generation
		/// keeps its documents and keeps serving reads while the next folds.
		/// </summary>
		[Fact]
		public async Task ResolveTwoGenerationsOfOneReadModelAsDistinctCoexistingDestinations()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddStore<FirstGenerationOrderView>(services);
			AddStore<SecondGenerationOrderView>(services);

			await using var host = services.BuildServiceProvider();

			var first = await ResolveIdentityAsync<FirstGenerationOrderView>(host);
			var second = await ResolveIdentityAsync<SecondGenerationOrderView>(host);

			first.ShouldBe("cosmosdb/orders-db/order-summaries/FirstGenerationOrderView");
			second.ShouldBe("cosmosdb/orders-db/order-summaries-v2/SecondGenerationOrderView");
			first.ShouldNotBe(second);
		}

		private static void AddStore<TProjection>(IServiceCollection services)
			where TProjection : class =>
			_ = services.AddCosmosDbProjectionStore<TProjection>(
				Endpoint,
				EmulatorKey,
				Database,
				options => options.ContainerName = Container);

		private static void OptionsFor<TProjection>(
			ServiceProvider provider, out CosmosDbProjectionStoreOptions options)
			where TProjection : class =>
			options = provider.GetRequiredService<IOptionsMonitor<CosmosDbProjectionStoreOptions>>()
				.Get(CosmosDbProjectionContainerConvention.GetOptionsName<TProjection>());

		private static async Task<string> ResolveIdentityAsync<TProjection>(ServiceProvider provider)
			where TProjection : class
		{
			await using var scope = provider.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();
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
