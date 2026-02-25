using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Elasticsearch;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchServiceCollectionExtensionsShould
{
	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Register_exporter_services()
	{
		var services = new ServiceCollection();

		services.AddElasticsearchAuditExporter(o =>
		{
			o.ElasticsearchUrl = "https://es.local:9200";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Configure_http_client_timeout_from_options()
	{
		var services = new ServiceCollection();

		services.AddElasticsearchAuditExporter(o =>
		{
			o.ElasticsearchUrl = "https://es.local:9200";
			o.Timeout = TimeSpan.FromSeconds(8);
		});

		using var provider = services.BuildServiceProvider();
		var exporter = provider.GetRequiredService<ElasticsearchAuditExporter>();

		var httpClientField = typeof(ElasticsearchAuditExporter)
			.GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		var httpClient = (HttpClient)httpClientField.GetValue(exporter)!;

		httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(8));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_services()
	{
		Should.Throw<ArgumentNullException>(() =>
			ElasticsearchServiceCollectionExtensions.AddElasticsearchAuditExporter(null!, _ => { }));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_configure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddElasticsearchAuditExporter(null!));
	}
}
