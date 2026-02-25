using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.SqlServer.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SqlServerAuditServiceCollectionExtensionsShould
{
	[Fact]
	public void Register_audit_store_services_with_action()
	{
		var services = new ServiceCollection();

		services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = "Server=localhost;Database=Audit";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(SqlServerAuditStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditStore));
	}

	[Fact]
	public void Throw_for_null_services_with_action()
	{
		Should.Throw<ArgumentNullException>(() =>
			SqlServerAuditServiceCollectionExtensions.AddSqlServerAuditStore(
				null!,
				_ => { }));
	}

	[Fact]
	public void Throw_for_null_configure_action()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerAuditStore((Action<SqlServerAuditOptions>)null!));
	}

	[Fact]
	public void Register_audit_store_services_with_options_instance()
	{
		var services = new ServiceCollection();
		var options = new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		};

		services.AddSqlServerAuditStore(options);

		services.ShouldContain(sd => sd.ServiceType == typeof(SqlServerAuditStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditStore));
	}

	[Fact]
	public void Throw_for_null_services_with_options_instance()
	{
		var options = new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit"
		};

		Should.Throw<ArgumentNullException>(() =>
			SqlServerAuditServiceCollectionExtensions.AddSqlServerAuditStore(
				null!,
				options));
	}

	[Fact]
	public void Throw_for_null_options_instance()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddSqlServerAuditStore((SqlServerAuditOptions)null!));
	}

	[Fact]
	public void Resolve_audit_store_from_service_provider_using_action_overload()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = "Server=localhost;Database=Audit";
			o.SchemaName = "audit_schema";
			o.TableName = "events";
		});

		using var provider = services.BuildServiceProvider();
		var auditStore = provider.GetRequiredService<IAuditStore>();
		var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqlServerAuditOptions>>().Value;

		auditStore.ShouldBeOfType<SqlServerAuditStore>();
		options.ConnectionString.ShouldBe("Server=localhost;Database=Audit");
		options.SchemaName.ShouldBe("audit_schema");
		options.TableName.ShouldBe("events");
	}

	[Fact]
	public void Resolve_options_from_service_provider_using_options_overload_copies_all_values()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var configured = new SqlServerAuditOptions
		{
			ConnectionString = "Server=localhost;Database=Audit",
			SchemaName = "custom_schema",
			TableName = "custom_table",
			BatchInsertSize = 777,
			RetentionPeriod = TimeSpan.FromDays(400),
			EnableRetentionEnforcement = false,
			RetentionCleanupInterval = TimeSpan.FromHours(12),
			RetentionCleanupBatchSize = 1234,
			CommandTimeoutSeconds = 42,
			UsePartitioning = true,
			EnableHashChain = false,
			EnableDetailedTelemetry = true
		};

		services.AddSqlServerAuditStore(configured);

		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqlServerAuditOptions>>().Value;
		var auditStore = provider.GetRequiredService<IAuditStore>();

		resolved.ConnectionString.ShouldBe(configured.ConnectionString);
		resolved.SchemaName.ShouldBe(configured.SchemaName);
		resolved.TableName.ShouldBe(configured.TableName);
		resolved.BatchInsertSize.ShouldBe(configured.BatchInsertSize);
		resolved.RetentionPeriod.ShouldBe(configured.RetentionPeriod);
		resolved.EnableRetentionEnforcement.ShouldBe(configured.EnableRetentionEnforcement);
		resolved.RetentionCleanupInterval.ShouldBe(configured.RetentionCleanupInterval);
		resolved.RetentionCleanupBatchSize.ShouldBe(configured.RetentionCleanupBatchSize);
		resolved.CommandTimeoutSeconds.ShouldBe(configured.CommandTimeoutSeconds);
		resolved.UsePartitioning.ShouldBe(configured.UsePartitioning);
		resolved.EnableHashChain.ShouldBe(configured.EnableHashChain);
		resolved.EnableDetailedTelemetry.ShouldBe(configured.EnableDetailedTelemetry);
		auditStore.ShouldBeOfType<SqlServerAuditStore>();
	}
}
