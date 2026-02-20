namespace Excalibur.Dispatch.AuditLogging.Splunk.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SplunkExporterOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.local:8088/services/collector"),
			HecToken = "test-token"
		};

		options.Index.ShouldBeNull();
		options.SourceType.ShouldBe("audit:dispatch");
		options.Source.ShouldBeNull();
		options.Host.ShouldBeNull();
		options.MaxBatchSize.ShouldBe(100);
		options.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.EnableCompression.ShouldBeTrue();
		options.ValidateCertificate.ShouldBeTrue();
		options.UseAck.ShouldBeFalse();
		options.Channel.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var options = new SplunkExporterOptions
		{
			HecEndpoint = new Uri("https://splunk.local:8088/services/collector"),
			HecToken = "my-token",
			Index = "main",
			SourceType = "custom:audit",
			Source = "my-app",
			Host = "my-host",
			MaxBatchSize = 50,
			RequestTimeout = TimeSpan.FromSeconds(60),
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromSeconds(2),
			EnableCompression = false,
			ValidateCertificate = false,
			UseAck = true,
			Channel = "my-channel"
		};

		options.HecToken.ShouldBe("my-token");
		options.Index.ShouldBe("main");
		options.SourceType.ShouldBe("custom:audit");
		options.Source.ShouldBe("my-app");
		options.Host.ShouldBe("my-host");
		options.MaxBatchSize.ShouldBe(50);
		options.EnableCompression.ShouldBeFalse();
		options.ValidateCertificate.ShouldBeFalse();
		options.UseAck.ShouldBeTrue();
		options.Channel.ShouldBe("my-channel");
	}
}
