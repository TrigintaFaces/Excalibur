namespace Excalibur.Dispatch.AuditLogging.Postgres.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class PostgresAuditOptionsShould
{
	[Fact]
	public void Have_sensible_defaults()
	{
		var options = new PostgresAuditOptions();

		options.ConnectionString.ShouldBe(string.Empty);
		options.SchemaName.ShouldBe("audit");
		options.TableName.ShouldBe("audit_events");
		options.BatchSize.ShouldBe(1000);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(7 * 365));
		options.RetentionCleanupBatchSize.ShouldBe(10000);
		options.CommandTimeoutSeconds.ShouldBe(30);
		options.EnableHashChain.ShouldBeTrue();
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = "Host=localhost;Database=audit",
			SchemaName = "custom_schema",
			TableName = "custom_events",
			BatchSize = 500,
			RetentionPeriod = TimeSpan.FromDays(365),
			RetentionCleanupBatchSize = 5000,
			CommandTimeoutSeconds = 60,
			EnableHashChain = false
		};

		options.ConnectionString.ShouldBe("Host=localhost;Database=audit");
		options.SchemaName.ShouldBe("custom_schema");
		options.TableName.ShouldBe("custom_events");
		options.BatchSize.ShouldBe(500);
		options.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
		options.RetentionCleanupBatchSize.ShouldBe(5000);
		options.CommandTimeoutSeconds.ShouldBe(60);
		options.EnableHashChain.ShouldBeFalse();
	}

	[Fact]
	public void Compute_fully_qualified_table_name_with_defaults()
	{
		var options = new PostgresAuditOptions();

		options.FullyQualifiedTableName.ShouldBe("\"audit\".\"audit_events\"");
	}

	[Fact]
	public void Compute_fully_qualified_table_name_with_custom_values()
	{
		var options = new PostgresAuditOptions
		{
			SchemaName = "my_schema",
			TableName = "my_table"
		};

		options.FullyQualifiedTableName.ShouldBe("\"my_schema\".\"my_table\"");
	}
}
