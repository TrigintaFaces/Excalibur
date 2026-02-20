using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.Splunk.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SplunkServiceCollectionExtensionsShould
{
	[Fact]
	public void Register_exporter_services_with_action()
	{
		var services = new ServiceCollection();

		services.AddSplunkAuditExporter(o =>
		{
			o.HecEndpoint = new Uri("https://splunk.local:8088/services/collector");
			o.HecToken = "test-token";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	public void Throw_for_null_services()
	{
		Should.Throw<ArgumentNullException>(() =>
			SplunkServiceCollectionExtensions.AddSplunkAuditExporter(
				null!,
				_ => { }));
	}

	[Fact]
	public void Throw_for_null_configure_action()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSplunkAuditExporter((Action<SplunkExporterOptions>)null!));
	}
}
