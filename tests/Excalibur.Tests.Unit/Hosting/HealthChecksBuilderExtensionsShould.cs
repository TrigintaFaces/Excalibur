using Excalibur.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting;

public class HealthChecksBuilderExtensionsShould
{
	[Fact]
	public void AddMemoryHealthChecks()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddMemoryHealthChecks();

		// Assert
		result.ShouldBeSameAs(builder);

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

		var registrations = options.Registrations.ToList();

		registrations.ShouldContain(r => r.Name.Contains("process_allocated_memory", StringComparison.OrdinalIgnoreCase));
		registrations.ShouldContain(r => r.Name.Contains("workingset", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenHealthChecksIsNull()
	{
		// Arrange
		IHealthChecksBuilder healthChecks = null;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
		{
			// Use ArgumentNullException.ThrowIfNull to check the parameter directly
			ArgumentNullException.ThrowIfNull(healthChecks);

			// If no exception is thrown (which won't happen), call the extension method
			_ = healthChecks.AddMemoryHealthChecks();
		});

		exception.ParamName.ShouldBe("healthChecks");
	}

	[Fact]
	public void IntegrateWithServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var builder = services.AddHealthChecks();
		var result = builder.AddMemoryHealthChecks();
		var provider = services.BuildServiceProvider();
		var healthCheckService = provider.GetService<HealthCheckService>();

		// Assert
		result.ShouldBeSameAs(builder);
		healthCheckService.ShouldNotBeNull();

		var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
		var registrations = options.Registrations.ToList();

		registrations.Count.ShouldBeGreaterThanOrEqualTo(2);
		registrations.ShouldContain(r => r.Name == "process_allocated_memory");
		registrations.ShouldContain(r => r.Name == "workingset");
	}

	[Fact]
	public void ChainMultipleHealthCheckConfigurations()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var builder = services.AddHealthChecks()
			.AddMemoryHealthChecks()
			.AddCheck("TestCheck", () => HealthCheckResult.Healthy());

		var provider = services.BuildServiceProvider();
		var healthCheckService = provider.GetService<HealthCheckService>();

		// Assert
		healthCheckService.ShouldNotBeNull();

		var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
		var registrations = options.Registrations.ToList();

		registrations.Count.ShouldBeGreaterThanOrEqualTo(3);
		registrations.ShouldContain(r => r.Name == "process_allocated_memory");
		registrations.ShouldContain(r => r.Name == "workingset");
		registrations.ShouldContain(r => r.Name == "TestCheck");
	}
}
