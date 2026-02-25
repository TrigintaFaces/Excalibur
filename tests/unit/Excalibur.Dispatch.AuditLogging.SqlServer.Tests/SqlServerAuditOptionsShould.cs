namespace Excalibur.Dispatch.AuditLogging.SqlServer.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SqlServerAuditOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new SqlServerAuditOptions();

		options.ConnectionString.ShouldBe(string.Empty);
		options.SchemaName.ShouldBe("audit");
		options.TableName.ShouldBe("AuditEvents");
		options.BatchInsertSize.ShouldBe(1000);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
		options.EnableRetentionEnforcement.ShouldBeTrue();
		options.RetentionCleanupInterval.ShouldBe(TimeSpan.FromDays(1));
		options.RetentionCleanupBatchSize.ShouldBe(10000);
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.UsePartitioning.ShouldBeFalse();
		options.EnableHashChain.ShouldBeTrue();
		options.EnableDetailedTelemetry.ShouldBeFalse();
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var options = new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit;Trusted_Connection=True",
			SchemaName = "custom_schema",
			TableName = "CustomEvents",
			BatchInsertSize = 500,
			RetentionPeriod = TimeSpan.FromDays(365),
			EnableRetentionEnforcement = false,
			RetentionCleanupInterval = TimeSpan.FromHours(12),
			RetentionCleanupBatchSize = 5000,
			CommandTimeoutSeconds = 60,
			UsePartitioning = true,
			EnableHashChain = false,
			EnableDetailedTelemetry = true
		};

		options.ConnectionString.ShouldBe("Server=localhost;Database=Audit;Trusted_Connection=True");
		options.SchemaName.ShouldBe("custom_schema");
		options.TableName.ShouldBe("CustomEvents");
		options.BatchInsertSize.ShouldBe(500);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
		options.EnableRetentionEnforcement.ShouldBeFalse();
		options.RetentionCleanupInterval.ShouldBe(TimeSpan.FromHours(12));
		options.RetentionCleanupBatchSize.ShouldBe(5000);
		options.CommandTimeoutSeconds.ShouldBe(60);
		options.UsePartitioning.ShouldBeTrue();
		options.EnableHashChain.ShouldBeFalse();
		options.EnableDetailedTelemetry.ShouldBeTrue();
	}

	[Fact]
	public void Compute_fully_qualified_table_name_with_defaults()
	{
		var options = new SqlServerAuditOptions();

		options.FullyQualifiedTableName.ShouldBe("[audit].[AuditEvents]");
	}

	[Fact]
	public void Compute_fully_qualified_table_name_with_custom_values()
	{
		var options = new SqlServerAuditOptions
		{
			SchemaName = "dbo",
			TableName = "MyEvents"
		};

		options.FullyQualifiedTableName.ShouldBe("[dbo].[MyEvents]");
	}
}
