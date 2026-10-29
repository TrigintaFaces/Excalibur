// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Data.CosmosDb.Tests.OptionsIdentity.Sales
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.CosmosDb.Tests.OptionsIdentity.Support
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.CosmosDb.Tests.CosmosDb.Projections
{
	using Excalibur.Data.CosmosDb.Projections;

	using SalesProjection = global::Excalibur.Data.CosmosDb.Tests.OptionsIdentity.Sales.EmployeeProjection;
	using SupportProjection = global::Excalibur.Data.CosmosDb.Tests.OptionsIdentity.Support.EmployeeProjection;

	/// <summary>
	/// Locks the Cosmos projection store's destination identity onto the database and container the writes
	/// resolve through, and locks the connection-level settings out of it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// No emulator is involved and none is needed. The identity is computed from bound options, so it is
	/// decided at registration time, before a single request is issued.
	/// </para>
	/// <para>
	/// Every arm registers BOTH projections into ONE service collection. That is the arm that carries the
	/// guarantee: the registration configures a NAMED options instance per projection type, so two
	/// projections in one container keep their own database and container. Registering them into one
	/// collection and reading two different identities is the only shape that can go red if the
	/// registration ever returns to the unnamed instance, where the last registration would decide both.
	/// </para>
	/// </remarks>
	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	[Trait("Database", "CosmosDb")]
	public sealed class ProjectionDestinationIdentityShould
	{
		private const string Endpoint = "https://localhost:8081/";

		private const string WestEndpoint = "https://cosmos-west.documents.azure.com:443/";

		// The well-known Cosmos emulator key. Not a credential: it is published by Microsoft, identical on
		// every emulator install, and nothing here opens a connection. pragma: allowlist secret
		private const string EmulatorKey =
			"C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

		[Theory]
		[InlineData("sales-db", "projections", "support-db", "projections")]
		[InlineData("people", "sales-employees", "people", "support-employees")]
		[InlineData("sales-db", "employees", "support-db", "employee-projections")]
		public async Task QualifyTheDatabaseAndContainerTogetherForSameNamedProjectionTypes(
			string salesDatabase, string salesContainer, string supportDatabase, string supportContainer)
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);
			typeof(SalesProjection).FullName.ShouldNotBe(typeof(SupportProjection).FullName);

			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddEndpointStore<SalesProjection>(services, salesDatabase, salesContainer);
			AddEndpointStore<SupportProjection>(services, supportDatabase, supportContainer);

			await using var host = services.BuildServiceProvider();

			var salesIdentity = await ResolveIdentityAsync<SalesProjection>(host);
			var supportIdentity = await ResolveIdentityAsync<SupportProjection>(host);

			salesIdentity.ShouldBe($"cosmosdb/{salesDatabase}/{salesContainer}/EmployeeProjection");
			supportIdentity.ShouldBe($"cosmosdb/{supportDatabase}/{supportContainer}/EmployeeProjection");
			salesIdentity.ShouldNotBe(
				supportIdentity,
				"a container name alone repeats across databases, so the identity must qualify both");
		}

		[Fact]
		public async Task KeepEachProjectionsDatabaseAndContainerWhenBothAreRegisteredInOneContainer()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddEndpointStore<OrderSummary>(services, "orders-db", "order-summaries");
			AddEndpointStore<CustomerProfile>(services, "crm-db", "customer-profiles");

			await using var host = services.BuildServiceProvider();

			var orderIdentity = await ResolveIdentityAsync<OrderSummary>(host);
			var customerIdentity = await ResolveIdentityAsync<CustomerProfile>(host);

			// Were the settings configured on the unnamed options instance, both stores would read the
			// LAST registration and both identities would be cosmosdb/crm-db/customer-profiles.
			orderIdentity.ShouldBe("cosmosdb/orders-db/order-summaries/OrderSummary");
			customerIdentity.ShouldBe("cosmosdb/crm-db/customer-profiles/CustomerProfile");
		}

		[Fact]
		public async Task ReadTheOptionsInstanceTheRegistrationConfigured()
		{
			// The store and the registration must derive the key from the same function. A test computing
			// its own key would stay green if the two ever diverged.
			CosmosDbProjectionStore<OrderSummary>.OptionsName
				.ShouldBe(CosmosDbProjectionContainerConvention.GetOptionsName<OrderSummary>());
			CosmosDbProjectionStore<OrderSummary>.OptionsName.ShouldBe(typeof(OrderSummary).FullName);

			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddEndpointStore<OrderSummary>(services, "orders-db", "order-summaries");

			await using var host = services.BuildServiceProvider();

			// The unnamed instance is deliberately left unconfigured, so it carries no account at all --
			// a store reading it could not even be constructed, let alone name the right destination.
			_ = Should.Throw<OptionsValidationException>(
				() => host.GetRequiredService<IOptions<CosmosDbProjectionStoreOptions>>().Value,
				"a projection registration must not configure the unnamed options instance");

			var named = host.GetRequiredService<IOptionsMonitor<CosmosDbProjectionStoreOptions>>()
				.Get(CosmosDbProjectionStore<OrderSummary>.OptionsName);
			named.DatabaseName.ShouldBe("orders-db");
			named.ContainerName.ShouldBe("order-summaries");

			(await ResolveIdentityAsync<OrderSummary>(host)).ShouldBe("cosmosdb/orders-db/order-summaries/OrderSummary");
		}

		[Fact]
		public async Task ExcludeTheAccountEndpointAndCredentialsFromTheLogicalDestinationIdentity()
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			var services = new ServiceCollection();
			_ = services.AddLogging();
			AddEndpointStore<SalesProjection>(services, "people", "shared-logical-target");
			AddConnectionStringStore<SupportProjection>(
				services,
				"people",
				"shared-logical-target",
				$"AccountEndpoint={WestEndpoint};AccountKey={EmulatorKey};");

			await using var host = services.BuildServiceProvider();

			var salesIdentity = await ResolveIdentityAsync<SalesProjection>(host);
			var supportIdentity = await ResolveIdentityAsync<SupportProjection>(host);

			salesIdentity.ShouldBe("cosmosdb/people/shared-logical-target/EmployeeProjection");
			supportIdentity.ShouldBe(
				salesIdentity,
				"the identity names the logical target; the consumer's subscription scope distinguishes "
					+ "different accounts, not endpoint or connection strings");
		}

		private static void AddEndpointStore<TProjection>(
			IServiceCollection services, string database, string container)
			where TProjection : class =>
			_ = services.AddCosmosDbProjectionStore<TProjection>(
				Endpoint,
				EmulatorKey,
				database,
				options => options.ContainerName = container);

		private static void AddConnectionStringStore<TProjection>(
			IServiceCollection services, string database, string container, string connectionString)
			where TProjection : class =>
			_ = services.AddCosmosDbProjectionStore<TProjection>(
				connectionString,
				database,
				options => options.ContainerName = container);

		private static async Task<string> ResolveIdentityAsync<TProjection>(ServiceProvider provider)
			where TProjection : class
		{
			await using var scope = provider.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;

			// The default interface implementation on IProjectionStore<T> answers any serviceType the store
			// itself satisfies. A store that never overrode GetService would hand back itself here, so the
			// capability would look present while naming nothing.
			(identity is IProjectionStore<TProjection>).ShouldBeFalse(
				"the identity must come from the store's own GetService, not the default interface implementation");

			return identity.Identity;
		}

		private sealed class OrderSummary
		{
			public string Id { get; set; } = string.Empty;
		}

		private sealed class CustomerProfile
		{
			public string Id { get; set; } = string.Empty;
		}
	}
}
