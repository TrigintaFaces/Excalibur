using Excalibur.Dispatch.Observability;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for ObservabilityOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ObservabilityOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new ObservabilityOptions();

		// Assert
		options.EnableMetrics.ShouldBeTrue();
		options.EnableTracing.ShouldBeTrue();
		options.EnableLogging.ShouldBeTrue();
		options.ActivitySourceName.ShouldBe("Excalibur.Dispatch");
		options.MeterName.ShouldBe(DispatchMetrics.MeterName);
		options.ServiceName.ShouldBe("Excalibur.Dispatch");
		options.ServiceVersion.ShouldBe("1.0.0");
		options.EnableDetailedTiming.ShouldBeFalse();
		options.IncludeSensitiveData.ShouldBeFalse();
	}

	[Fact]
	public void EnableMetrics_CanBeDisabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableMetrics = false;

		// Assert
		options.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void EnableTracing_CanBeDisabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableTracing = false;

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void EnableLogging_CanBeDisabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableLogging = false;

		// Assert
		options.EnableLogging.ShouldBeFalse();
	}

	[Fact]
	public void ActivitySourceName_CanBeCustomized()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.ActivitySourceName = "MyApp";

		// Assert
		options.ActivitySourceName.ShouldBe("MyApp");
	}

	[Fact]
	public void MeterName_CanBeCustomized()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.MeterName = "MyApp.Metrics";

		// Assert
		options.MeterName.ShouldBe("MyApp.Metrics");
	}

	[Fact]
	public void ServiceName_CanBeCustomized()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.ServiceName = "OrderService";

		// Assert
		options.ServiceName.ShouldBe("OrderService");
	}

	[Fact]
	public void ServiceVersion_CanBeCustomized()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.ServiceVersion = "2.1.0";

		// Assert
		options.ServiceVersion.ShouldBe("2.1.0");
	}

	[Fact]
	public void EnableDetailedTiming_CanBeEnabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.EnableDetailedTiming = true;

		// Assert
		options.EnableDetailedTiming.ShouldBeTrue();
	}

	[Fact]
	public void IncludeSensitiveData_CanBeEnabled()
	{
		// Arrange
		var options = new ObservabilityOptions();

		// Act
		options.IncludeSensitiveData = true;

		// Assert
		options.IncludeSensitiveData.ShouldBeTrue();
	}
}
