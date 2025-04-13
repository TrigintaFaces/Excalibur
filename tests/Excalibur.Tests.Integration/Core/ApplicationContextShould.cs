using Excalibur.Core;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Mothers.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.Core;

public class ApplicationContextShould : SqlServerPersistenceOnlyTestBase, IAsyncDisposable
{
	public ApplicationContextShould(SqlServerContainerFixture fixture, ITestOutputHelper output)
		: base(fixture, output)
	{
		// Add configuration with application context values
		var configValues = new Dictionary<string, string?>
		{
			{ "ApplicationName", "IntegrationTest" },
			{ "ApplicationSystemName", "integration-test" },
			{ "ServiceAccountName", "integration-service-account" },
			{ "ConfigValue1", "BaseValue" },
			{ "ConfigValue2", "%ConfigValue1%-Extended" },
			{ "ConnectionString", "Server=%DbServer%;Database=%DbName%;User Id=%DbUser%;Password=%DbPassword%;" }
		};

		// Initialize ApplicationContext with our test values
		ApplicationContextMother.Initialize(configValues);
	}

	public async ValueTask DisposeAsync()
	{
		// Reset after each test
		ApplicationContextMother.Reset();
		await base.DisposeAsync().ConfigureAwait(true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void InitializeFromConfigurationValues()
	{
		// Assert
		ApplicationContext.ApplicationName.ShouldBe("IntegrationTest");
		ApplicationContext.ApplicationSystemName.ShouldBe("integration-test");
		ApplicationContext.ServiceAccountName.ShouldBe("integration-service-account");
	}

	[Fact]
	public void ExpandNestedPlaceholdersInConfiguration()
	{
		// Act
		var result = ApplicationContext.Get("ConfigValue2");

		// Assert
		result.ShouldBe("BaseValue-Extended");
	}

	[Fact]
	public void ExpandEnvironmentVariablesInCombinationWithContextValues()
	{
		// Arrange
		var originalValues = new Dictionary<string, string?> { { "DbServer", "test-server" }, { "DbName", "test-db" } };

		// Initialize with our values
		ApplicationContextMother.Initialize(originalValues);

		// Set environment variables for other placeholders
		Environment.SetEnvironmentVariable("DbUser", "sa");
		Environment.SetEnvironmentVariable("DbPassword", "P@ssw0rd!");

		// Act
		var connectionString = ApplicationContext.Expand(ApplicationContext.Get("ConnectionString"));

		// Assert
		connectionString.ShouldBe("Server=test-server;Database=test-db;User Id=sa;Password=P@ssw0rd!;");

		// Cleanup
		Environment.SetEnvironmentVariable("DbUser", null);
		Environment.SetEnvironmentVariable("DbPassword", null);
	}

	[Fact]
	public void AllowAccessingPropertiesAcrossComponents()
	{
		// Arrange
		var service = GetRequiredService<TestService>();

		// Act
		var applicationName = service.GetApplicationName();

		// Assert
		applicationName.ShouldBe("IntegrationTest");
	}

	[Fact]
	public void MaintainStateAcrossDifferentServices()
	{
		// Arrange
		var firstService = GetRequiredService<TestService>();
		var secondService = GetRequiredService<AnotherTestService>();

		// Act
		var firstValue = firstService.GetApplicationSystemName();
		var secondValue = secondService.GetApplicationSystemName();

		// Assert
		firstValue.ShouldBe("integration-test");
		secondValue.ShouldBe("integration-test");
		firstValue.ShouldBe(secondValue);
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		base.AddServices(services, configuration);

		// Register our test services
		_ = services.AddTransient<TestService>();
		_ = services.AddTransient<AnotherTestService>();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			ApplicationContextMother.Reset();
		}

		base.Dispose(disposing);
	}

	// Service classes for testing
	private sealed class TestService
	{
		public string GetApplicationName() => ApplicationContext.ApplicationName;

		public string GetApplicationSystemName() => ApplicationContext.ApplicationSystemName;
	}

	private sealed class AnotherTestService
	{
		public string GetApplicationSystemName() => ApplicationContext.ApplicationSystemName;
	}
}
