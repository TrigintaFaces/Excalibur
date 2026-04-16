using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

		services.AddElasticsearchAuditExporter(es =>
		{
			es.NodeUri(new Uri("https://es.local:9200"));
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
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
			services.AddElasticsearchAuditExporter((Action<IAuditLoggingElasticsearchBuilder>)null!));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Register_exporter_services_with_BindConfiguration()
	{
		var services = new ServiceCollection();

		services.AddElasticsearchAuditExporter(es =>
		{
			es.BindConfiguration("AuditLogging:Elasticsearch");
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	public void Register_sink_services_with_builder()
	{
		var services = new ServiceCollection();

		services.AddElasticsearchAuditSink(es =>
		{
			es.NodeUri(new Uri("https://es.local:9200"));
		});

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<ElasticsearchAuditSinkOptions>));
	}
}
