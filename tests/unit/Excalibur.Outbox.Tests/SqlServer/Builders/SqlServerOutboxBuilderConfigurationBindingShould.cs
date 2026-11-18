// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.SqlServer.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBuilderConfigurationBindingShould : UnitTestBase
{
	private const string Section = "Outbox:SqlServer";
	private const string ConfiguredConnection = "Server=localhost;Database=Configured;Integrated Security=true;TrustServerCertificate=true;";
	private const string ExplicitConnection = "Server=localhost;Database=Explicit;Integrated Security=true;TrustServerCertificate=true;";

	[Fact]
	public void BindScalarAndReadOnlyNestedOptionsFromActualConfiguration()
	{
		var configuration = CreateConfiguration();
		using var configurationLifetime = (IDisposable)configuration;
		using var provider = CreateProvider(configuration, sql => sql.BindConfiguration(Section));
		var options = provider.GetRequiredService<IOptionsMonitor<SqlServerOutboxOptions>>().CurrentValue;
		options.ConnectionString.ShouldBe(ConfiguredConnection);
		options.ApplicationName.ShouldBe("ConfiguredApp");
		options.ProcessorId.ShouldBe("configured-worker");
		options.Tables.SchemaName.ShouldBe("Messaging");
		options.Tables.OutboxTableName.ShouldBe("Requests");
		options.Tables.QualifiedOutboxTableName.ShouldBe("[Messaging].[Requests]");
		options.Processing.DefaultBatchSize.ShouldBe(27);
		options.Processing.UseRowLocking.ShouldBeFalse();
	}

	[Fact]
	public void ReloadScalarAndNestedOptionsAndNotifyMonitor()
	{
		var configuration = CreateConfiguration();
		using var configurationLifetime = (IDisposable)configuration;
		using var provider = CreateProvider(configuration, sql => sql.BindConfiguration(Section));
		var monitor = provider.GetRequiredService<IOptionsMonitor<SqlServerOutboxOptions>>();
		var original = monitor.CurrentValue;
		SqlServerOutboxOptions? observed = null;
		using var subscription = monitor.OnChange((value, _) => observed = value);
		configuration[$"{Section}:ApplicationName"] = "ReloadedApp";
		configuration[$"{Section}:Tables:SchemaName"] = "Reloaded";
		configuration[$"{Section}:Processing:DefaultBatchSize"] = "41";
		configuration.Reload();
		var reloaded = monitor.CurrentValue;
		reloaded.ShouldNotBeSameAs(original);
		observed.ShouldBeSameAs(reloaded);
		reloaded.ApplicationName.ShouldBe("ReloadedApp");
		reloaded.Tables.SchemaName.ShouldBe("Reloaded");
		reloaded.Processing.DefaultBatchSize.ShouldBe(41);
		original.ApplicationName.ShouldBe("ConfiguredApp");
		original.Tables.SchemaName.ShouldBe("Messaging");
		original.Processing.DefaultBatchSize.ShouldBe(27);
	}

	[Fact]
	public void IgnoreUnknownConfigurationKeysWithDefaultBinderPolicy()
	{
		var configuration = CreateConfiguration();
		using var configurationLifetime = (IDisposable)configuration;
		configuration[$"{Section}:FutureSetting"] = "ignored";
		using var provider = CreateProvider(configuration, sql => sql.BindConfiguration(Section));
		provider.GetRequiredService<IOptionsMonitor<SqlServerOutboxOptions>>()
			.CurrentValue.ApplicationName.ShouldBe("ConfiguredApp");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void PreservePublicLastConnectionModeWinsAcrossConfigurationReload(bool bindLast)
	{
		var configuration = CreateConfiguration();
		using var configurationLifetime = (IDisposable)configuration;
		using var provider = CreateProvider(configuration, sql =>
		{
			if (bindLast)
			{
				sql.ConnectionString(ExplicitConnection).BindConfiguration(Section);
			}
			else
			{
				sql.BindConfiguration(Section).ConnectionString(ExplicitConnection);
			}
		});
		var monitor = provider.GetRequiredService<IOptionsMonitor<SqlServerOutboxOptions>>();
		monitor.CurrentValue.ConnectionString.ShouldBe(bindLast ? ConfiguredConnection : ExplicitConnection);
		configuration[$"{Section}:ConnectionString"] = ConfiguredConnection.Replace("Configured", "Reloaded", StringComparison.Ordinal);
		configuration.Reload();
		monitor.CurrentValue.ConnectionString.ShouldBe(bindLast
			? ConfiguredConnection.Replace("Configured", "Reloaded", StringComparison.Ordinal)
			: ExplicitConnection);
	}

	private static IConfigurationRoot CreateConfiguration() => new ConfigurationBuilder()
		.AddInMemoryCollection(new Dictionary<string, string?>
		{
			[$"{Section}:ConnectionString"] = ConfiguredConnection,
			[$"{Section}:ApplicationName"] = "ConfiguredApp",
			[$"{Section}:ProcessorId"] = "configured-worker",
			[$"{Section}:Tables:SchemaName"] = "Messaging",
			[$"{Section}:Tables:OutboxTableName"] = "Requests",
			[$"{Section}:Processing:DefaultBatchSize"] = "27",
			[$"{Section}:Processing:UseRowLocking"] = "false",
		}).Build();

	private static ServiceProvider CreateProvider(IConfiguration configuration, Action<ISqlServerOutboxBuilder> configure)
	{
		var services = new ServiceCollection();
		services.AddSingleton(configuration);
		services.AddExcaliburOutbox(outbox => outbox.UseSqlServer(configure));
		return services.BuildServiceProvider();
	}
}
