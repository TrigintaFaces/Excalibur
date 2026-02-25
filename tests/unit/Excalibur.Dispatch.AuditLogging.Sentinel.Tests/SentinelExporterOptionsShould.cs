using Excalibur.Dispatch.AuditLogging.Sentinel;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelExporterOptionsShould
{
	[Fact]
	public void Have_correct_defaults()
	{
		// Arrange & Act
		var options = new SentinelExporterOptions { WorkspaceId = "ws-id", SharedKey = "key" };

		// Assert
		options.LogType.ShouldBe("DispatchAudit");
		options.AzureResourceId.ShouldBeNull();
		options.TimeGeneratedField.ShouldBe("timestamp");
		options.MaxBatchSize.ShouldBe(500);
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Allow_custom_log_type()
	{
		// Arrange & Act
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "ws-id",
			SharedKey = "key",
			LogType = "CustomAudit"
		};

		// Assert
		options.LogType.ShouldBe("CustomAudit");
	}

	[Fact]
	public void Allow_custom_azure_resource_id()
	{
		// Arrange & Act
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "ws-id",
			SharedKey = "key",
			AzureResourceId = "/subscriptions/sub-id/resourceGroups/rg/providers/ns/type/name"
		};

		// Assert
		options.AzureResourceId.ShouldNotBeNull();
	}

	[Fact]
	public void Allow_null_time_generated_field()
	{
		// Arrange & Act
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "ws-id",
			SharedKey = "key",
			TimeGeneratedField = null
		};

		// Assert
		options.TimeGeneratedField.ShouldBeNull();
	}

	[Fact]
	public void Allow_custom_batch_size()
	{
		// Arrange & Act
		var options = new SentinelExporterOptions
		{
			WorkspaceId = "ws-id",
			SharedKey = "key",
			MaxBatchSize = 100
		};

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}
}
