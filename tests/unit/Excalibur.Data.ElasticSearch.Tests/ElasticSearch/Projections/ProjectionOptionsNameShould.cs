// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Sales
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Support
{
	internal sealed class EmployeeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections
{
	using SalesProjection = global::Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Sales.EmployeeProjection;
	using SupportProjection = global::Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Support.EmployeeProjection;

	[Trait("Category", "Unit")]
	[Trait("Component", "Projections")]
	public sealed class ProjectionOptionsNameShould
	{
		[Fact]
		public async Task IsolateElasticOptionsIndexesAndClientsForTypesWithTheSameSimpleName()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddElasticSearchProjectionStore<SalesProjection>(o =>
			{
				o.NodeUri = "http://localhost:9200";
				o.Index.IndexName = "sales-employees";
			});
			services.AddElasticSearchProjectionStore<SupportProjection>(o =>
			{
				o.NodeUri = "http://localhost:9201";
				o.Index.IndexName = "support-employees";
			});
			await using var provider = services.BuildServiceProvider();
			var monitor = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
			monitor.Get(ElasticSearchProjectionIndexConvention.GetOptionsName<SalesProjection>()).NodeUri
				.ShouldBe("http://localhost:9200");
			monitor.Get(ElasticSearchProjectionIndexConvention.GetOptionsName<SupportProjection>()).NodeUri
				.ShouldBe("http://localhost:9201");
			await using var first = provider.CreateAsyncScope();
			await using var second = provider.CreateAsyncScope();
			var sales = first.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>();
			var support = first.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>();
			AssertStoresAndClients(sales, support,
				second.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>(),
				second.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>());
			Field<string>(sales, "_indexName").ShouldBe("projections-sales-employees");
			Field<string>(support, "_indexName").ShouldBe("projections-support-employees");
		}

		[Fact]
		public async Task IsolateOpenSearchOptionsIndexesAndClientsForTypesWithTheSameSimpleName()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddOpenSearchProjectionStore<SalesProjection>(o =>
			{
				o.NodeUri = "http://localhost:9200";
				o.IndexName = "sales-employees";
			});
			services.AddOpenSearchProjectionStore<SupportProjection>(o =>
			{
				o.NodeUri = "http://localhost:9201";
				o.IndexName = "support-employees";
			});
			await using var provider = services.BuildServiceProvider();
			var monitor = provider.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
			monitor.Get(typeof(SalesProjection).FullName!).NodeUri.ShouldBe("http://localhost:9200");
			monitor.Get(typeof(SupportProjection).FullName!).NodeUri.ShouldBe("http://localhost:9201");
			await using var first = provider.CreateAsyncScope();
			await using var second = provider.CreateAsyncScope();
			var sales = first.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>();
			var support = first.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>();
			AssertStoresAndClients(sales, support,
				second.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>(),
				second.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>());
			Field<string>(sales, "_indexName").ShouldBe("projections-sales-employees");
			Field<string>(support, "_indexName").ShouldBe("projections-support-employees");
		}

		[Fact]
		public void KeepIndexNamingIndependentFromTheOptionsBucket()
		{
			ElasticSearchProjectionIndexConvention.GetOptionsName<SalesProjection>()
				.ShouldBe(typeof(SalesProjection).FullName);
			ElasticSearchProjectionIndexConvention.GetOptionsName<SalesProjection>()
				.ShouldNotBe(ElasticSearchProjectionIndexConvention.GetOptionsName<SupportProjection>());
			ElasticSearchProjectionIndexConvention.GetIndexName<SalesProjection>(new ElasticSearchProjectionStoreOptions())
				.ShouldBe("projections-employeeprojection");
		}

		[Fact]
		public void AllowTheSameProjectionToBeRegisteredTwice()
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddElasticSearchProjectionStore<SalesProjection>(o => o.NodeUri = "http://localhost:9200");
			services.AddElasticSearchProjectionStore<SalesProjection>(o => o.NodeUri = "http://localhost:9201");
		}

		private static void AssertStoresAndClients(object sales, object support, object laterSales, object laterSupport)
		{
			ReferenceEquals(sales, laterSales).ShouldBeFalse();
			ReferenceEquals(support, laterSupport).ShouldBeFalse();
			var salesClient = Field<object>(sales, "_client");
			var supportClient = Field<object>(support, "_client");
			salesClient.ShouldNotBeNull();
			supportClient.ShouldNotBeNull();
			ReferenceEquals(salesClient, supportClient).ShouldBeFalse();
			ReferenceEquals(salesClient, Field<object>(laterSales, "_client")).ShouldBeTrue();
			ReferenceEquals(supportClient, Field<object>(laterSupport, "_client")).ShouldBeTrue();
		}

		private static T Field<T>(object store, string name) =>
			(T)store.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
	}
}
