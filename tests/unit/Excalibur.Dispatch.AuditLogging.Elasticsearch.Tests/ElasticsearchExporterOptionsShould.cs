namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ElasticsearchExporterOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new ElasticsearchExporterOptions
		{
			ElasticsearchUrl = "https://es.local:9200"
		};

		options.IndexPrefix.ShouldBe("dispatch-audit");
		options.BulkBatchSize.ShouldBe(500);
		options.RefreshPolicy.ShouldBe("false");
		options.ApiKey.ShouldBeNull();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var options = new ElasticsearchExporterOptions
		{
			ElasticsearchUrl = "https://es.local:9200",
			IndexPrefix = "custom-audit",
			BulkBatchSize = 100,
			RefreshPolicy = "wait_for",
			ApiKey = "my-api-key",
			MaxRetryAttempts = 5,
			RetryBaseDelay = TimeSpan.FromSeconds(2),
			Timeout = TimeSpan.FromSeconds(60)
		};

		options.ElasticsearchUrl.ShouldBe("https://es.local:9200");
		options.IndexPrefix.ShouldBe("custom-audit");
		options.BulkBatchSize.ShouldBe(100);
		options.RefreshPolicy.ShouldBe("wait_for");
		options.ApiKey.ShouldBe("my-api-key");
	}
}
