using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.SqlServer.Tests;

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

	/// <summary>
	/// The store attests the tenancy mechanism it actually implements, as part of the same registration act.
	/// </summary>
	/// <remarks>
	/// IAuditStore carries [TenantOwned] and is gated by AddMultiTenancy, so a host wiring this store beside
	/// the row discriminator is refused at startup unless the marker is present. The marker is obtainable
	/// only from the dep-gated seam -- its single member is internal to Excalibur.Dispatch.Abstractions, so no
	/// provider can register a look-alike -- which makes this arm the thing that goes red if the registration
	/// regresses to a bare TryAddSingleton. Scoping rather than partitioned is the correct attestation: every
	/// query this store builds binds the ambient tenant term as a scope, never as a caller-supplied filter.
	/// </remarks>
	[Fact]
	public void Attest_ambient_tenant_scoping_for_the_audit_contract()
	{
		var services = new ServiceCollection();

		services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = "Server=localhost;Database=Audit";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(ITenantScopingCapability<IAuditStore>));
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
			Retention = new()
			{
				RetentionPeriod = TimeSpan.FromDays(400),
				EnableRetentionEnforcement = false,
				CleanupInterval = TimeSpan.FromHours(12),
				CleanupBatchSize = 1234
			},
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
		resolved.Retention.RetentionPeriod.ShouldBe(configured.Retention.RetentionPeriod);
		resolved.Retention.EnableRetentionEnforcement.ShouldBe(configured.Retention.EnableRetentionEnforcement);
		resolved.Retention.CleanupInterval.ShouldBe(configured.Retention.CleanupInterval);
		resolved.Retention.CleanupBatchSize.ShouldBe(configured.Retention.CleanupBatchSize);
		resolved.CommandTimeoutSeconds.ShouldBe(configured.CommandTimeoutSeconds);
		resolved.UsePartitioning.ShouldBe(configured.UsePartitioning);
		resolved.EnableHashChain.ShouldBe(configured.EnableHashChain);
		resolved.EnableDetailedTelemetry.ShouldBe(configured.EnableDetailedTelemetry);
		auditStore.ShouldBeOfType<SqlServerAuditStore>();
	}
}