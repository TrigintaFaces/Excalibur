// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

using SalesProjection = Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Sales.EmployeeProjection;
using SupportProjection = Excalibur.Data.ElasticSearch.Tests.OptionsIdentity.Support.EmployeeProjection;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class ProjectionDestinationIdentityShould
{
	[Theory]
	[InlineData("Sales", "Employees", "Support", "Employees")]
	[InlineData("People", "Employees", "People", "EmployeesV2")]
	[InlineData("", "SalesEmployees", "", "SupportEmployees")]
	[InlineData("Sales-V2", "EmployeeProjection", "Support-V2", "EmployeeProjection")]
	public async Task IncludeTheResolvedBoundedContextPrefixAndExplicitIndexNameForSameNamedTypes(
		string salesPrefix, string salesName, string supportPrefix, string supportName)
	{
		typeof(SalesProjection).Name.ShouldBe(typeof(SupportProjection).Name);
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElasticSearchProjectionStore<SalesProjection>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexPrefix = salesPrefix;
			options.Index.IndexName = salesName;
		});
		services.AddElasticSearchProjectionStore<SupportProjection>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexPrefix = supportPrefix;
			options.Index.IndexName = supportName;
		});
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var salesStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>();
		var supportStore = scope.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>();
		var salesIdentity = (IProjectionDestinationIdentity)salesStore.GetService(typeof(IProjectionDestinationIdentity))!;
		var supportIdentity = (IProjectionDestinationIdentity)supportStore.GetService(typeof(IProjectionDestinationIdentity))!;
		var monitor = provider.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		salesIdentity.Identity.ShouldBe("elasticsearch/" + ElasticSearchProjectionIndexConvention.GetIndexName<SalesProjection>(
			monitor.Get(ElasticSearchProjectionIndexConvention.GetOptionsName<SalesProjection>())));
		supportIdentity.Identity.ShouldBe("elasticsearch/" + ElasticSearchProjectionIndexConvention.GetIndexName<SupportProjection>(
			monitor.Get(ElasticSearchProjectionIndexConvention.GetOptionsName<SupportProjection>())));
		salesIdentity.Identity.ShouldNotBe(supportIdentity.Identity);
		(salesIdentity is IProjectionStore<SalesProjection>).ShouldBeFalse();
	}

	[Fact]
	public async Task ExcludeNodeTopologyAndCredentialsFromTheLogicalDestinationIdentity()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElasticSearchProjectionStore<SalesProjection>(options =>
		{
			options.NodeUri = "http://localhost:9200";
			options.Index.IndexName = "shared-logical-target";
		});
		services.AddElasticSearchProjectionStore<SupportProjection>(options =>
		{
			options.NodeUri = "http://localhost:9201";
			options.Index.IndexName = "shared-logical-target";
		});
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		var first = (IProjectionDestinationIdentity)scope.ServiceProvider.GetRequiredService<IProjectionStore<SalesProjection>>()
			.GetService(typeof(IProjectionDestinationIdentity))!;
		var second = (IProjectionDestinationIdentity)scope.ServiceProvider.GetRequiredService<IProjectionStore<SupportProjection>>()
			.GetService(typeof(IProjectionDestinationIdentity))!;
		first.Identity.ShouldBe(second.Identity, "the consumer's SubscriptionScope distinguishes different clusters, not node URI strings");
	}
}
