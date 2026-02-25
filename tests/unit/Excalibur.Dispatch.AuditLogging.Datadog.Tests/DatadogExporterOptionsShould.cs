using Excalibur.Dispatch.AuditLogging.Datadog;

namespace Excalibur.Dispatch.AuditLogging.Datadog.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DatadogExporterOptionsShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key" };

		// Assert
		options.Site.ShouldBe("datadoghq.com");
		options.Service.ShouldBe("dispatch-audit");
		options.Source.ShouldBe("dispatch");
		options.Hostname.ShouldBeNull();
		options.Tags.ShouldBeNull();
		options.MaxBatchSize.ShouldBe(500);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseCompression.ShouldBeTrue();
	}

	[Fact]
	public void Allow_custom_site_configuration()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key", Site = "datadoghq.eu" };

		// Assert
		options.Site.ShouldBe("datadoghq.eu");
	}

	[Fact]
	public void Allow_custom_hostname()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key", Hostname = "my-host" };

		// Assert
		options.Hostname.ShouldBe("my-host");
	}

	[Fact]
	public void Allow_custom_tags()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key", Tags = "env:prod,team:platform" };

		// Assert
		options.Tags.ShouldBe("env:prod,team:platform");
	}

	[Fact]
	public void Allow_custom_batch_size()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key", MaxBatchSize = 100 };

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Allow_disabling_compression()
	{
		// Arrange & Act
		var options = new DatadogExporterOptions { ApiKey = "test-key", UseCompression = false };

		// Assert
		options.UseCompression.ShouldBeFalse();
	}
}
