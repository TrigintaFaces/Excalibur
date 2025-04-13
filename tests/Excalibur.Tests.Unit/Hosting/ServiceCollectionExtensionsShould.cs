using Excalibur.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting;

public class ServiceCollectionExtensionsShould
{
	[Fact]
	public void AddExcaliburHealthChecksToServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburHealthChecks();

		// Assert
		result.ShouldBeSameAs(services);

		// Verify health check services were registered
		services.Any(s => s.ServiceType == typeof(HealthCheckService)).ShouldBeTrue();
	}

	[Fact]
	public void InvokeCustomHealthChecksConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var configCalled = false;

		// Act
		_ = services.AddExcaliburHealthChecks(builder =>
		{
			configCalled = true;
			_ = builder.AddCheck("CustomCheck", () => HealthCheckResult.Healthy());
		});

		// Assert
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburHealthChecks());

		exception.ParamName.ShouldBe("services");
	}
}
