using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsAuditServiceCollectionExtensionsShould
{
	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Register_exporter_services()
	{
		var services = new ServiceCollection();

		services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Configure_http_client_timeout_from_options()
	{
		var services = new ServiceCollection();

		services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
			o.Timeout = TimeSpan.FromSeconds(7);
		});

		using var provider = services.BuildServiceProvider();
		var exporter = provider.GetRequiredService<AwsCloudWatchAuditExporter>();

		var httpClientField = typeof(AwsCloudWatchAuditExporter).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		var httpClient = (HttpClient)httpClientField.GetValue(exporter)!;

		httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(7));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_services()
	{
		Should.Throw<ArgumentNullException>(() =>
			AwsAuditServiceCollectionExtensions.AddAwsAuditExporter(null!, _ => { }));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Throw_for_null_configure()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddAwsAuditExporter(null!));
	}
}
