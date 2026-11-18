// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.DataProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Quartz;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>Verifies public job binding, nested CDC settings, validation and configuration reload.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Configuration")]
[Trait("Pattern", "Regression")]
public sealed class JobsConfigurationBindingShould : UnitTestBase
{
	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public async Task BindPublicSchedulingConfigurationWithoutStartingAScheduler(bool cdc, bool disabled)
	{
		var configuration = BuildConfiguration();
		var section = cdc ? "Jobs:CdcJob" : "Jobs:DataProcessingJob";
		configuration[$"{section}:Disabled"] = disabled.ToString();
		var services = new ServiceCollection();
		services.AddQuartz(builder =>
		{
			builder.ConfigureScheduler(options => options.InstanceName = $"binding-{Guid.NewGuid():N}");
			if (cdc) { CdcJob.ConfigureJob(builder, configuration); }
			else { DataProcessingJob.ConfigureJob(builder, configuration); }
		});
		services.AddLogging();
		await using var provider = services.BuildServiceProvider();
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(TestContext.Current.CancellationToken);
		try
		{
			(await scheduler.GetStatus(TestContext.Current.CancellationToken)).ShouldBe(SchedulerStatus.Created);
			var key = new JobKey(cdc ? "cdc-bound" : "data-bound", "bound-group");
			var job = await scheduler.GetJobDetail(key, TestContext.Current.CancellationToken);
			var triggers = await scheduler.GetTriggersOfJob(key, TestContext.Current.CancellationToken);
			if (disabled)
			{
				job.ShouldBeNull();
				triggers.ShouldBeEmpty();
				return;
			}

			job.ShouldNotBeNull();
			job.Key.ShouldBe(key);
			job.JobType.ShouldBe(cdc ? typeof(CdcJob) : typeof(DataProcessingJob));
			var trigger = triggers.ShouldHaveSingleItem();
			trigger.JobKey.ShouldBe(key);
			trigger.ShouldBeAssignableTo<ICronTrigger>().CronExpressionString.ShouldBe("0 0/7 * * * ?");
		}
		finally
		{
			await scheduler.Shutdown(waitForJobsToComplete: false, cancellationToken: TestContext.Current.CancellationToken);
		}
	}

	[Theory]
	[InlineData(true, "00:20:00", "00:30:00", HealthStatus.Healthy)]
	[InlineData(false, "00:20:00", "00:30:00", HealthStatus.Healthy)]
	[InlineData(true, "00:02:00", "00:20:00", HealthStatus.Degraded)]
	[InlineData(false, "00:02:00", "00:20:00", HealthStatus.Degraded)]
	public async Task BindPublicHealthThresholds(bool cdc, string degraded, string unhealthy, HealthStatus expected)
	{
		var configuration = BuildConfiguration();
		var section = cdc ? "Jobs:CdcJob" : "Jobs:DataProcessingJob";
		configuration[$"{section}:DegradedThreshold"] = degraded;
		configuration[$"{section}:UnhealthyThreshold"] = unhealthy;
		var name = cdc ? "cdc-bound" : "data-bound";
		// Ten minutes old: far from every tested boundary, with no delay or private-field inspection.
		var tracker = new JobHeartbeatTracker(new FixedClock(DateTimeOffset.UtcNow.AddMinutes(-10)));
		tracker.RecordHeartbeat(name);
		var services = new ServiceCollection();
		services.AddSingleton(tracker);
		var health = services.AddHealthChecks();
		if (cdc) { CdcJob.ConfigureHealthChecks(health, configuration); }
		else { DataProcessingJob.ConfigureHealthChecks(health, configuration); }
		using var provider = services.BuildServiceProvider();
		var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.ShouldHaveSingleItem();
		registration.Name.ShouldBe($"{name}HealthCheck");
		var result = await registration.Factory(provider).CheckHealthAsync(
			new HealthCheckContext { Registration = registration }, CancellationToken.None);
		result.Status.ShouldBe(expected);
	}

	[Fact]
	public void BindNestedCdcOptionsAndReloadWithoutLosingInitOnlyConfiguration()
	{
		var configuration = BuildConfiguration();
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>();
		var before = monitor.CurrentValue;
		before.DatabaseConfigs.Count.ShouldBe(2);
		AssertDatabase(before.DatabaseConfigs[0], "sales", 257);
		AssertDatabase(before.DatabaseConfigs[1], "audit", 258);
		before.DatabaseConfigs[0].Tables.Count.ShouldBe(2);
		before.DatabaseConfigs[0].Tables[1].TableName.ShouldBe("dbo.Order");
		before.DatabaseConfigs[0].CaptureInstanceToTableNameMap["orders_v2"].ShouldBe("dbo.Order");
		configuration["Jobs:CdcJob:DatabaseConfigs:0:QueueSize"] = "1029";
		configuration["Jobs:CdcJob:DatabaseConfigs:0:Tables:0:CaptureInstance"] = "accounts_v3";
		configuration.Reload();
		var after = monitor.CurrentValue;
		after.ShouldNotBeSameAs(before);
		after.DatabaseConfigs[0].QueueSize.ShouldBe(1029);
		after.DatabaseConfigs[0].CaptureInstances.ShouldContain("accounts_v3");
		before.DatabaseConfigs[0].QueueSize.ShouldBe(257);
		before.DatabaseConfigs[0].CaptureInstances.ShouldContain("accounts_v2");
	}

	[Theory]
	[InlineData("JobName")]
	[InlineData("CronSchedule")]
	public void PreserveCdcOptionsValidation(string property)
	{
		var configuration = BuildConfiguration();
		configuration[$"Jobs:CdcJob:{property}"] = "";
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		var error = Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>().CurrentValue);
		error.Failures.ShouldContain(failure => failure.Contains(property, StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("DatabaseName")]
	[InlineData("DatabaseConnectionIdentifier")]
	[InlineData("StateConnectionIdentifier")]
	[InlineData("Tables:0:TableName")]
	public void ReportTheNestedPathWhenCdcConfigurationIsInvalid(string property)
	{
		var configuration = BuildConfiguration();
		configuration[$"Jobs:CdcJob:DatabaseConfigs:0:{property}"] = " ";
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		var error = Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>().CurrentValue);
		var expectedPath = property == "Tables:0:TableName"
			? "DatabaseConfigs[0].Tables[0].TableName"
			: $"DatabaseConfigs[0].{property}";
		error.Failures.ShouldContain(failure => failure.Contains(expectedPath, StringComparison.Ordinal));
	}

	[Fact]
	public void RejectAConfiguredDatabaseWithoutAnyTrackedTables()
	{
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(BuildConfiguration());
		services.PostConfigure<CdcJobOptions>(options => options.DatabaseConfigs[0].Tables.Clear());
		using var provider = services.BuildServiceProvider();
		var error = Should.Throw<OptionsValidationException>(() =>
			provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>().CurrentValue);
		error.Failures.ShouldContain(failure => failure.Contains("DatabaseConfigs[0].Tables", StringComparison.Ordinal));
	}

	private static void AssertDatabase(DatabaseOptions database, string name, int queueSize)
	{
		database.DatabaseName.ShouldBe(name);
		database.DatabaseConnectionIdentifier.ShouldBe($"{name}-read");
		database.StateConnectionIdentifier.ShouldBe($"{name}-state");
		database.QueueSize.ShouldBe(queueSize);
		database.ProducerBatchSize.ShouldBe(37);
		database.ConsumerBatchSize.ShouldBe(19);
		database.StopOnMissingTableHandler.ShouldBeTrue();
		database.Tables[0].TableName.ShouldBe("dbo.Account");
		database.Tables[0].CaptureInstance.ShouldBe("accounts_v2");
		var recovery = database.RecoveryOptions.ShouldNotBeNull();
		recovery.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.Throw);
		recovery.MaxRecoveryAttempts.ShouldBe(8);
		recovery.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(4));
		recovery.EnableStructuredLogging.ShouldBeFalse();
	}

	private static IConfigurationRoot BuildConfiguration()
	{
		var values = new Dictionary<string, string?>(StringComparer.Ordinal);
		foreach (var (section, name) in new[] { ("CdcJob", "cdc-bound"), ("DataProcessingJob", "data-bound") })
		{
			values[$"Jobs:{section}:JobName"] = name;
			values[$"Jobs:{section}:JobGroup"] = "bound-group";
			values[$"Jobs:{section}:CronSchedule"] = "0 0/7 * * * ?";
		}
		for (var i = 0; i < 2; i++)
		{
			var name = i == 0 ? "sales" : "audit";
			var prefix = $"Jobs:CdcJob:DatabaseConfigs:{i}";
			values[$"{prefix}:DatabaseName"] = name;
			values[$"{prefix}:DatabaseConnectionIdentifier"] = $"{name}-read";
			values[$"{prefix}:StateConnectionIdentifier"] = $"{name}-state";
			values[$"{prefix}:QueueSize"] = (257 + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
			values[$"{prefix}:ProducerBatchSize"] = "37";
			values[$"{prefix}:ConsumerBatchSize"] = "19";
			values[$"{prefix}:StopOnMissingTableHandler"] = "true";
			values[$"{prefix}:Tables:0:TableName"] = "dbo.Account";
			values[$"{prefix}:Tables:0:CaptureInstance"] = "accounts_v2";
			values[$"{prefix}:RecoveryOptions:RecoveryStrategy"] = "Throw";
			values[$"{prefix}:RecoveryOptions:MaxRecoveryAttempts"] = "8";
			values[$"{prefix}:RecoveryOptions:RecoveryAttemptDelay"] = "00:00:04";
			values[$"{prefix}:RecoveryOptions:EnableStructuredLogging"] = "false";
		}
		values["Jobs:CdcJob:DatabaseConfigs:0:Tables:1:TableName"] = "dbo.Order";
		values["Jobs:CdcJob:DatabaseConfigs:0:Tables:1:CaptureInstance"] = "orders_v2";
		return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
	}

	private sealed class FixedClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => now;
	}
}

