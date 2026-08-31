// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.AuditLogging.Elasticsearch.Tests;

/// <summary>
/// The audit sink entry points registered an internal writer that implemented no contract: nothing in
/// the framework resolved it and, being internal, a consumer could not resolve it either. The exporter
/// is the reachable path and covers the same single-event write, so the sink surface is gone. These
/// lock that removal and that the exporter a consumer is now directed to still resolves.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "AuditLogging")]
public sealed class AuditSinkEntryPointsShould
{
	[Theory]
	[InlineData(typeof(OpenSearchServiceCollectionExtensions), "AddOpenSearchAuditSink")]
	[InlineData(typeof(ElasticsearchServiceCollectionExtensions), "AddElasticsearchAuditSink")]
	public void NotBeOffered(Type extensions, string methodName) =>
		extensions.GetMethod(methodName).ShouldBeNull(
			$"{methodName} registered a writer nothing could resolve; the exporter is the reachable path");

	[Theory]
	[InlineData("Excalibur.AuditLogging.OpenSearch.OpenSearchAuditSink")]
	[InlineData("Excalibur.AuditLogging.OpenSearch.OpenSearchAuditSinkOptions")]
	[InlineData("Excalibur.AuditLogging.Elasticsearch.ElasticsearchAuditSink")]
	[InlineData("Excalibur.AuditLogging.Elasticsearch.ElasticsearchAuditSinkOptions")]
	public void NotDeclareTheSinkTypes(string typeName)
	{
		var assemblies = new[]
		{
			typeof(OpenSearchServiceCollectionExtensions).Assembly,
			typeof(ElasticsearchServiceCollectionExtensions).Assembly,
		};

		assemblies
			.Select(a => a.GetType(typeName))
			.Where(static t => t is not null)
			.ShouldBeEmpty($"{typeName} was only reachable through the removed sink entry point");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void StillResolveTheExporterConsumersAreDirectedTo(bool openSearch)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = openSearch
			? services.AddOpenSearchAuditExporter(b => b.NodeUri(new Uri("https://localhost:9200")).IndexName("audit"))
			: services.AddElasticsearchAuditExporter(b => b.NodeUri(new Uri("https://localhost:9200")).IndexName("audit"));

		using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

		provider.GetRequiredService<IAuditLogExporter>().ShouldNotBeNull();
	}
}
