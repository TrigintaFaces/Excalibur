using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GoogleCloudServiceCollectionExtensionsShould
{
	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Register_exporter_services()
	{
		var services = new ServiceCollection();

		services.AddGoogleCloudAuditExporter(o =>
		{
			o.ProjectId = "test-project";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Configure_http_client_timeout_from_options()
	{
		var services = new ServiceCollection();

		services.AddGoogleCloudAuditExporter(o =>
		{
			o.ProjectId = "test-project";
			o.Timeout = TimeSpan.FromSeconds(9);
		});

		using var provider = services.BuildServiceProvider();
		var exporter = provider.GetRequiredService<GoogleCloudLoggingAuditExporter>();

		var httpClientField = typeof(GoogleCloudLoggingAuditExporter).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		var httpClient = (HttpClient)httpClientField.GetValue(exporter)!;

		httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(9));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_services()
	{
		Should.Throw<ArgumentNullException>(() =>
			GoogleCloudServiceCollectionExtensions.AddGoogleCloudAuditExporter(null!, _ => { }));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_configure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddGoogleCloudAuditExporter(null!));
	}
}
