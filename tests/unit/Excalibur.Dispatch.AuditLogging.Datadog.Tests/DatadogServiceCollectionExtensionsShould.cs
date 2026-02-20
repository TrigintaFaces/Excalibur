using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogServiceCollectionExtensionsShould
{
	[Fact]
	public void Register_audit_exporter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "test-key";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	public void Register_options_with_validation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "test-key";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IOptionsChangeTokenSource<DatadogExporterOptions>)
								  || sd.ServiceType == typeof(IConfigureOptions<DatadogExporterOptions>));
	}

	[Fact]
	public void Throw_for_null_services()
	{
		// Act & Assert
#pragma warning disable IL2026, IL3050
		Should.Throw<ArgumentNullException>(() =>
			DatadogServiceCollectionExtensions.AddDatadogAuditExporter(null!, _ => { }));
#pragma warning restore IL2026, IL3050
	}

	[Fact]
	public void Throw_for_null_configure_action()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
#pragma warning disable IL2026, IL3050
		Should.Throw<ArgumentNullException>(() =>
			services.AddDatadogAuditExporter(null!));
#pragma warning restore IL2026, IL3050
	}
}
