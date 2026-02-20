namespace Excalibur.Dispatch.AuditLogging.GoogleCloud.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GoogleCloudAuditOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new GoogleCloudAuditOptions { ProjectId = "test" };

		options.LogName.ShouldBe("dispatch-audit");
		options.ResourceType.ShouldBe("global");
		options.Labels.ShouldBeNull();
		options.MaxBatchSize.ShouldBe(500);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var labels = new Dictionary<string, string> { ["env"] = "prod" };
		var options = new GoogleCloudAuditOptions
		{
			ProjectId = "my-project",
			LogName = "custom-log",
			ResourceType = "k8s_container",
			Labels = labels,
			MaxBatchSize = 100,
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromSeconds(2),
			Timeout = TimeSpan.FromSeconds(60)
		};

		options.ProjectId.ShouldBe("my-project");
		options.LogName.ShouldBe("custom-log");
		options.ResourceType.ShouldBe("k8s_container");
		options.Labels.ShouldBeSameAs(labels);
		options.MaxBatchSize.ShouldBe(100);
	}
}
