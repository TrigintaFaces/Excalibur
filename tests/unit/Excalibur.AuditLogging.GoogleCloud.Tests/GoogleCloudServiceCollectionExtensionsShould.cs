using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.GoogleCloud.Tests;

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

		services.AddGoogleCloudAuditExporter(gcp =>
		{
			gcp.ProjectId("test-project");
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
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
			services.AddGoogleCloudAuditExporter((Action<IAuditLoggingGoogleCloudBuilder>)null!));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void Register_exporter_with_BindConfiguration()
	{
		var services = new ServiceCollection();

		services.AddGoogleCloudAuditExporter(gcp =>
		{
			gcp.BindConfiguration("AuditLogging:GoogleCloud");
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}
}
