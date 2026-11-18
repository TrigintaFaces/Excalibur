// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.DataProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Quartz;

namespace Excalibur.Dispatch.Aot.Sample;

/// <summary>Exercises the public Jobs configuration paths in an actual native executable.</summary>
internal static class JobsBindingSmoke
{
	internal static async Task RunAsync()
	{
		Require(!RuntimeFeature.IsDynamicCodeSupported, "This control requires an actual NativeAOT executable.");
		Require(!JsonSerializer.IsReflectionEnabledByDefault, "Reflection-based JSON must remain disabled.");
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
		foreach (var cdc in new[] { true, false })
		{
			await VerifySchedulingAsync(cdc, disabled: false, timeout.Token);
			await VerifySchedulingAsync(cdc, disabled: true, timeout.Token);
			VerifyHealthRegistration(cdc);
		}
		VerifyNestedOptionsAndReload();
		VerifyDefaultOptions();
		foreach (var key in new[] { "JobName", "CronSchedule", "DatabaseConfigs:0:DatabaseName", "DatabaseConfigs:0:Tables:0:TableName" })
		{
			VerifyValidation(key);
		}
		Console.WriteLine("PASS jobs-binding-smoke: native public scheduling/health, nested CDC options, defaults, validation and reload.");
	}

	private static async Task VerifySchedulingAsync(bool cdc, bool disabled, CancellationToken token)
	{
		var configuration = BuildConfiguration();
		var section = cdc ? "Jobs:CdcJob" : "Jobs:DataProcessingJob";
		configuration[$"{section}:Disabled"] = disabled.ToString();
		var services = new ServiceCollection();
		services.AddQuartz(builder =>
		{
			builder.ConfigureScheduler(options => options.InstanceName = $"native-jobs-binding-{Guid.NewGuid():N}");
			if (cdc) { CdcJob.ConfigureJob(builder, configuration); }
			else { DataProcessingJob.ConfigureJob(builder, configuration); }
		});
		services.AddLogging();
		await using var provider = services.BuildServiceProvider();
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
		try
		{
			Require(await scheduler.GetStatus(token) == SchedulerStatus.Created, "Scheduler must never start.");
			var key = new JobKey(cdc ? "cdc-bound" : "data-bound", "bound-group");
			var job = await scheduler.GetJobDetail(key, token);
			var triggers = await scheduler.GetTriggersOfJob(key, token);
			if (disabled)
			{
				Require(job is null && triggers.Count == 0, "Disabled job/trigger was registered.");
				return;
			}
			Require(job is not null && job.Key.Equals(key), "Configured job identity was lost.");
			Require(job!.JobType == (cdc ? typeof(CdcJob) : typeof(DataProcessingJob)), "Configured job type was lost.");
			Require(triggers.Count == 1 && triggers.Single().JobKey.Equals(key), "Configured trigger was lost.");
			Require(triggers.Single() is ICronTrigger cron && cron.CronExpressionString == "0 0/7 * * * ?", "Cron binding changed.");
		}
		finally
		{
			// Cleanup gets its own bounded budget even if the primary verification token expired.
			using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await scheduler.Shutdown(waitForJobsToComplete: false, cancellationToken: cleanup.Token);
		}
	}

	private static void VerifyHealthRegistration(bool cdc)
	{
		var services = new ServiceCollection();
		var health = services.AddHealthChecks();
		var configuration = BuildConfiguration();
		if (cdc) { CdcJob.ConfigureHealthChecks(health, configuration); }
		else { DataProcessingJob.ConfigureHealthChecks(health, configuration); }
		using var provider = services.BuildServiceProvider();
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		Require(registrations.Count == 1 && registrations.Single().Name == (cdc ? "cdc-boundHealthCheck" : "data-boundHealthCheck"), "Health binding lost the configured name.");
	}

	private static void VerifyNestedOptionsAndReload()
	{
		var configuration = BuildConfiguration();
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>();
		var before = monitor.CurrentValue;
		Require(before.DatabaseConfigs.Count == 2, "Nested database count changed.");
		VerifyDatabase(before.DatabaseConfigs[0], "sales", 257);
		VerifyDatabase(before.DatabaseConfigs[1], "audit", 258);
		Require(before.DatabaseConfigs[0].Tables.Count == 2 && before.DatabaseConfigs[0].CaptureInstanceToTableNameMap["orders_v2"] == "dbo.Order", "Second table or derived mapping was lost.");
		configuration["Jobs:CdcJob:DatabaseConfigs:0:QueueSize"] = "1029";
		configuration["Jobs:CdcJob:DatabaseConfigs:0:Tables:0:CaptureInstance"] = "accounts_v3";
		configuration.Reload();
		var after = monitor.CurrentValue;
		Require(!ReferenceEquals(after, before) && after.DatabaseConfigs[0].QueueSize == 1029, "Options reload failed.");
		Require(after.DatabaseConfigs[0].CaptureInstances.Contains("accounts_v3", StringComparer.Ordinal), "Reloaded mapping remained stale.");
		Require(before.DatabaseConfigs[0].QueueSize == 257 && before.DatabaseConfigs[0].CaptureInstances.Contains("accounts_v2", StringComparer.Ordinal), "Reload mutated an earlier options instance.");
	}

	private static void VerifyDatabase(DatabaseOptions database, string name, int queueSize)
	{
		Require(database.DatabaseName == name && database.DatabaseConnectionIdentifier == $"{name}-read" && database.StateConnectionIdentifier == $"{name}-state", "Database identifiers were lost.");
		Require(database.QueueSize == queueSize && database.ProducerBatchSize == 37 && database.ConsumerBatchSize == 19 && database.StopOnMissingTableHandler, "Batch or missing-handler options were lost.");
		Require(database.Tables[0].TableName == "dbo.Account" && database.Tables[0].CaptureInstance == "accounts_v2", "Tracked table settings were lost.");
		var recovery = database.RecoveryOptions;
		Require(recovery is not null && recovery.RecoveryStrategy == StalePositionRecoveryStrategy.Throw && recovery.MaxRecoveryAttempts == 8 && recovery.RecoveryAttemptDelay == TimeSpan.FromSeconds(4) && !recovery.EnableStructuredLogging, "Recovery binding was lost.");
	}

	private static void VerifyDefaultOptions()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Jobs:CdcJob:JobName"] = "defaults", ["Jobs:CdcJob:CronSchedule"] = "0 0/7 * * * ?",
			["Jobs:CdcJob:DatabaseConfigs:0:DatabaseName"] = "default-db",
			["Jobs:CdcJob:DatabaseConfigs:0:DatabaseConnectionIdentifier"] = "read",
			["Jobs:CdcJob:DatabaseConfigs:0:StateConnectionIdentifier"] = "state",
			["Jobs:CdcJob:DatabaseConfigs:0:Tables:0:TableName"] = "dbo.Default",
		}).Build();
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		var actual = provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>().CurrentValue;
		var expected = new DatabaseOptions("default-db", "read", "state");
		var database = actual.DatabaseConfigs.Single();
		Require(actual.JobGroup == "Default" && !actual.Disabled, "Base defaults changed.");
		Require(database.QueueSize == expected.QueueSize && database.ProducerBatchSize == expected.ProducerBatchSize && database.ConsumerBatchSize == expected.ConsumerBatchSize && database.StopOnMissingTableHandler == expected.StopOnMissingTableHandler, "Database defaults changed during binding.");
		Require(database.RecoveryOptions is null && database.CaptureInstanceToTableNameMap["dbo.Default"] == "dbo.Default", "Default recovery or identity mapping changed.");
	}

	private static void VerifyValidation(string key)
	{
		var configuration = BuildConfiguration();
		configuration[$"Jobs:CdcJob:{key}"] = " ";
		var services = new ServiceCollection();
		services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();
		try
		{
			_ = provider.GetRequiredService<IOptionsMonitor<CdcJobOptions>>().CurrentValue;
		}
		catch (OptionsValidationException error)
		{
			var expected = key.Replace("DatabaseConfigs:0:", "DatabaseConfigs[0].", StringComparison.Ordinal).Replace("Tables:0:", "Tables[0].", StringComparison.Ordinal);
			Require(error.Failures.Any(failure => failure.Contains(expected, StringComparison.Ordinal)), "Validation lost the failing path.");
			return;
		}
		throw new InvalidOperationException($"Invalid configuration was accepted: {key}.");
	}

	private static void Require(bool condition, string message)
	{
		if (!condition) { throw new InvalidOperationException(message); }
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

}
