namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsAuditOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new AwsAuditOptions
		{
			LogGroupName = "test",
			Region = "us-east-1"
		};

		options.BatchSize.ShouldBe(500);
		options.StreamName.ShouldBeNull();
		options.ServiceUrl.ShouldBeNull();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var options = new AwsAuditOptions
		{
			LogGroupName = "my-group",
			Region = "eu-west-1",
			StreamName = "my-stream",
			BatchSize = 100,
			ServiceUrl = "https://custom.local",
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromSeconds(2),
			Timeout = TimeSpan.FromSeconds(60)
		};

		options.LogGroupName.ShouldBe("my-group");
		options.Region.ShouldBe("eu-west-1");
		options.StreamName.ShouldBe("my-stream");
		options.BatchSize.ShouldBe(100);
		options.ServiceUrl.ShouldBe("https://custom.local");
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(2));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
	}
}
