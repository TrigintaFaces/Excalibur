// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.Tests.DynamoDbOptionsIdentity.Sales
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.DynamoDbOptionsIdentity.Support
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable
{
	/// <summary>One of two DIFFERENTLY-named projection types that legitimately share one table.</summary>
	internal sealed class CustomerOrderProjection
	{
		public string Id { get; set; } = string.Empty;
	}

	/// <summary>The other. Same table, different destination -- the type prefix is the only separator.</summary>
	internal sealed class WarehouseStockProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.Tests.DynamoDb.Projections
{
	using Amazon.DynamoDBv2;
	using Amazon.Runtime;

	using Excalibur.Data.DynamoDb.Projections;
	using Excalibur.EventSourcing;

	using OrderProjection = global::Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.CustomerOrderProjection;
	using SalesProjection = global::Excalibur.Data.Tests.DynamoDbOptionsIdentity.Sales.EmployeeProjection;
	using StockProjection = global::Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.WarehouseStockProjection;
	using SupportProjection = global::Excalibur.Data.Tests.DynamoDbOptionsIdentity.Support.EmployeeProjection;

	/// <summary>
	/// Binds the DynamoDB projection store's destination-identity capability, resolved through real DI.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This provider is a documented single-table design: the partition key is <c>{type}#{id}</c> and
	/// scans filter on the projection type, so TWO projection types legitimately share ONE table and are
	/// nonetheless two distinct destinations. The identity therefore carries both the table and the
	/// projection type name -- <c>dynamodb/{table}/{type}</c>. An implementation emitting only
	/// <c>dynamodb/{table}</c> passes a naive two-table test and fails the single-table arm below.
	/// </para>
	/// <para>
	/// The type segment is the FULLY-QUALIFIED type name, so two projection classes with the same class
	/// name in different namespaces sharing one table are two distinct destinations -- and, because the
	/// same discriminator is the partition-key prefix and the scan filter, two distinct sets of items.
	/// That pair is asserted below rather than stated as a limitation.
	/// </para>
	/// <para>
	/// Store settings are registered per projection type, so two projections configured with DIFFERENT
	/// tables in ONE container each keep their own -- also asserted below.
	/// </para>
	/// <para>
	/// Every arm resolves the store from a real container via the production registration path, and
	/// asserts the capability came from the store's own explicit <c>GetService</c> rather than from the
	/// default interface implementation on <c>IProjectionStore{TProjection}</c> (which answers
	/// <c>serviceType.IsInstanceOfType(this) ? this : null</c>). No live infrastructure: the identity is
	/// computable from bound options, so nothing here connects to DynamoDB.
	/// </para>
	/// </remarks>
	[Trait("Category", TestCategories.Unit)]
	[Trait("Component", "Data")]
	[Trait("Database", "DynamoDb")]
	public sealed class ProjectionDestinationIdentityShould
	{
		/// <summary>
		/// SAFETY, the single-table case: two projection types on ONE table are two destinations.
		/// </summary>
		[Theory]
		[InlineData("Projections")]
		[InlineData("SharedProjections")]
		[InlineData("acme-prod-readmodels")]
		public async Task SeparateTwoProjectionTypesSharingOneTableByTheirTypePrefix(string tableName)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(A.Fake<IAmazonDynamoDB>());
			services.AddDynamoDbProjectionStore<OrderProjection>(options => options.TableName = tableName);
			services.AddDynamoDbProjectionStore<StockProjection>(options => options.TableName = tableName);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();
			var orderStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<OrderProjection>>();
			var stockStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<StockProjection>>();
			var orderIdentity = (IProjectionDestinationIdentity)orderStore.GetService(typeof(IProjectionDestinationIdentity))!;
			var stockIdentity = (IProjectionDestinationIdentity)stockStore.GetService(typeof(IProjectionDestinationIdentity))!;

			orderIdentity.Identity.ShouldBe(
				$"dynamodb/{tableName}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.CustomerOrderProjection");
			stockIdentity.Identity.ShouldBe(
				$"dynamodb/{tableName}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.WarehouseStockProjection");
			orderIdentity.Identity.ShouldNotBe(
				stockIdentity.Identity,
				"the single-table design puts both types in one table, so the type prefix is what makes them two destinations");
			(orderIdentity is IProjectionStore<OrderProjection>).ShouldBeFalse();
			(stockIdentity is IProjectionStore<StockProjection>).ShouldBeFalse();
		}

		/// <summary>
		/// SAFETY, the table case: the same projection type on two tables is two destinations.
		/// </summary>
		/// <remarks>
		/// The pair is deliberately two classes with the SAME name in different namespaces -- so the type
		/// segment is identical in both identities and only the table can separate them.
		/// </remarks>
		[Theory]
		[InlineData("SalesProjections", "SupportProjections")]
		[InlineData("Projections", "Projections-V2")]
		[InlineData("acme-prod-readmodels", "acme-staging-readmodels")]
		public async Task IncludeTheConfiguredTableNameForSameNamedTypesOnDifferentTables(
			string salesTable, string supportTable)
		{
			typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);

			var salesIdentity = await ResolveIdentityAsync<SalesProjection>(salesTable, A.Fake<IAmazonDynamoDB>());
			var supportIdentity = await ResolveIdentityAsync<SupportProjection>(supportTable, A.Fake<IAmazonDynamoDB>());

			salesIdentity.Identity.ShouldBe(
				$"dynamodb/{salesTable}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.Sales.EmployeeProjection");
			supportIdentity.Identity.ShouldBe(
				$"dynamodb/{supportTable}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.Support.EmployeeProjection");
			salesIdentity.Identity.ShouldNotBe(supportIdentity.Identity);
			(salesIdentity is IProjectionStore<SalesProjection>).ShouldBeFalse();
		}

		/// <summary>
		/// LIVENESS: the identity names the LOGICAL target, so a connection-level difference does not
		/// split one destination into two.
		/// </summary>
		[Fact]
		public async Task ExcludeEndpointRegionAndCredentialsFromTheLogicalDestinationIdentity()
		{
			const string Table = "shared-logical-target";

			using var firstClient = new AmazonDynamoDBClient(
				new BasicAWSCredentials("first-access-key", "first-secret"),
				new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000", AuthenticationRegion = "us-east-1" });
			using var secondClient = new AmazonDynamoDBClient(
				new BasicAWSCredentials("second-access-key", "second-secret"),
				new AmazonDynamoDBConfig { ServiceURL = "http://127.0.0.1:9000", AuthenticationRegion = "eu-west-2" });

			var first = await ResolveIdentityAsync<OrderProjection>(Table, firstClient);
			var second = await ResolveIdentityAsync<OrderProjection>(Table, secondClient);

			first.Identity.ShouldBe(
				second.Identity,
				"the identity names the LOGICAL target; the consumer's SubscriptionScope distinguishes different accounts or regions, not endpoint strings");
			first.Identity.ShouldBe(
				$"dynamodb/{Table}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.CustomerOrderProjection");
		}

		/// <summary>
		/// SAFETY, the same-simple-name case: two projection classes named alike in different namespaces
		/// sharing ONE table are two destinations -- and two sets of items.
		/// </summary>
		/// <remarks>
		/// The identity segment is also the partition-key prefix and the scan-filter value, so a collision
		/// here is not a reporting nuisance: the two projections would write the same keys and read each
		/// other's items, overwriting one read model with the other wherever their ids coincide, with
		/// nothing raised and nothing downstream able to notice.
		/// </remarks>
		[Theory]
		[InlineData("Projections")]
		[InlineData("acme-prod-readmodels")]
		public async Task SeparateSameNamedProjectionTypesFromDifferentNamespacesSharingOneTable(string tableName)
		{
			typeof(SalesProjection).Name.ShouldBe(
				typeof(SupportProjection).Name,
				"the arm is only meaningful while the two classes share a simple name");

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(A.Fake<IAmazonDynamoDB>());
			services.AddDynamoDbProjectionStore<SalesProjection>(options => options.TableName = tableName);
			services.AddDynamoDbProjectionStore<SupportProjection>(options => options.TableName = tableName);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();
			var salesIdentity = (IProjectionDestinationIdentity)scope.ServiceProvider
				.GetRequiredService<IProjectionStore<SalesProjection>>()
				.GetService(typeof(IProjectionDestinationIdentity))!;
			var supportIdentity = (IProjectionDestinationIdentity)scope.ServiceProvider
				.GetRequiredService<IProjectionStore<SupportProjection>>()
				.GetService(typeof(IProjectionDestinationIdentity))!;

			salesIdentity.Identity.ShouldNotBe(
				supportIdentity.Identity,
				"two same-named projection classes sharing one table write the same partition keys unless "
				+ "the discriminator is fully qualified -- silent cross-projection overwrite");
			salesIdentity.Identity.ShouldBe(
				$"dynamodb/{tableName}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.Sales.EmployeeProjection");
			supportIdentity.Identity.ShouldBe(
				$"dynamodb/{tableName}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.Support.EmployeeProjection");
		}

		/// <summary>
		/// LIVENESS: two projection types registered in ONE container with DIFFERENT tables each keep
		/// their own table.
		/// </summary>
		/// <remarks>
		/// Previously inexpressible: the registration configured the unnamed options instance, so the last
		/// delegate won for both stores and per-projection table configuration had no effect at all.
		/// </remarks>
		[Theory]
		[InlineData("orders-readmodels", "stock-readmodels")]
		[InlineData("Projections", "Projections-V2")]
		public async Task GiveEachProjectionItsOwnTableWhenTwoAreRegisteredInOneContainer(
			string orderTable, string stockTable)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(A.Fake<IAmazonDynamoDB>());
			services.AddDynamoDbProjectionStore<OrderProjection>(options => options.TableName = orderTable);
			services.AddDynamoDbProjectionStore<StockProjection>(options => options.TableName = stockTable);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();
			var orderIdentity = (IProjectionDestinationIdentity)scope.ServiceProvider
				.GetRequiredService<IProjectionStore<OrderProjection>>()
				.GetService(typeof(IProjectionDestinationIdentity))!;
			var stockIdentity = (IProjectionDestinationIdentity)scope.ServiceProvider
				.GetRequiredService<IProjectionStore<StockProjection>>()
				.GetService(typeof(IProjectionDestinationIdentity))!;

			orderIdentity.Identity.ShouldBe(
				$"dynamodb/{orderTable}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.CustomerOrderProjection",
				"each projection reads the table its own configuration delegate set");
			stockIdentity.Identity.ShouldBe(
				$"dynamodb/{stockTable}/Excalibur.Data.Tests.DynamoDbOptionsIdentity.SingleTable.WarehouseStockProjection",
				"the second registration must not have been overwritten by -- or overwrite -- the first");
		}

		/// <summary>
		/// Resolves the destination identity for one projection type through the production registration
		/// path. One container per call keeps the arms independent; registrations are per-type named, so
		/// sharing a container would work too -- the two-tables-one-container arm asserts exactly that.
		/// </summary>
		private static async Task<IProjectionDestinationIdentity> ResolveIdentityAsync<TProjection>(
			string tableName, IAmazonDynamoDB client)
			where TProjection : class
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(client);
			services.AddDynamoDbProjectionStore<TProjection>(options => options.TableName = tableName);

			await using var provider = services.BuildServiceProvider();
			await using var scope = provider.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>();

			return (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;
		}
	}
}
