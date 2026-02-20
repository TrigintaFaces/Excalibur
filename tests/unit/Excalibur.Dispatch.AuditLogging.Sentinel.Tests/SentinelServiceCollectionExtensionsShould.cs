using Excalibur.Dispatch.AuditLogging.Sentinel;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelServiceCollectionExtensionsShould
{
	[Fact]
	public void Register_audit_exporter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "test-ws";
			options.SharedKey = "dGVzdC1rZXk=";
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
		services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "test-ws";
			options.SharedKey = "dGVzdC1rZXk=";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IConfigureOptions<SentinelExporterOptions>));
	}

	[Fact]
	public void Throw_for_null_services()
	{
		// Act & Assert
#pragma warning disable IL2026, IL3050
		Should.Throw<ArgumentNullException>(() =>
			SentinelServiceCollectionExtensions.AddSentinelAuditExporter(null!, _ => { }));
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
			services.AddSentinelAuditExporter(null!));
#pragma warning restore IL2026, IL3050
	}
}
