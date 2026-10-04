// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;
using Excalibur.Data.DataProcessing;
using Excalibur.Jobs;
using Excalibur.Jobs.Core;
using Excalibur.Jobs.DataProcessing;
using Excalibur.Jobs.Quartz;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class QuartzSqlSmoke
{
	internal static async Task RunAsync()
	{
		var connection = Environment.GetEnvironmentVariable("PROCESSING_SQL")
			?? throw new InvalidOperationException("Set PROCESSING_SQL to a disposable SQL Server database.");
		await using (var sql = new SqlConnection(connection))
		{
			await sql.OpenAsync().ConfigureAwait(false);
			await using var command = sql.CreateCommand();
			command.CommandText = """
				IF OBJECT_ID('dbo.NativeTasks') IS NULL
				CREATE TABLE dbo.NativeTasks(DataTaskId uniqueidentifier PRIMARY KEY, CreatedAt datetimeoffset NOT NULL,
				RecordType nvarchar(256) NOT NULL, Attempts int NOT NULL DEFAULT 0, MaxAttempts int NOT NULL,
				CompletedCount bigint NOT NULL DEFAULT 0, FetchCursor nvarchar(512) NULL, ProcessedCursor nvarchar(512) NULL);
				""";
			_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<JobHeartbeatTracker>();
		services.AddSingleton<SmokeSignals>();
		services.AddSingleton<IDataProcessorRegistry, SmokeRegistry>();
		services.AddSingleton<IDataOrchestrationManager>(sp => new DataOrchestrationManager(
			() => new SqlConnection(connection), sp.GetRequiredService<IDataProcessorRegistry>(), sp,
			Microsoft.Extensions.Options.Options.Create(new DataProcessingOptions { SchemaName = "dbo", TableName = "NativeTasks" }),
			NullLogger<DataOrchestrationManager>.Instance));
		var jobs = new JobConfigurator(services);
		jobs.AddJob<SmokePlainJob>("0 0 0 1 1 ?", "native-plain");
		jobs.AddJob<SmokeContextJob, SmokeContext>("0 0 0 1 1 ?", new("persisted-context"),
			SmokeJson.Default.SmokeContext, "native-context");
		services.AddQuartz(q =>
		{
			q.ConfigureScheduler(o => o.InstanceName = "ExcaliburNativeSmoke");
			q.UsePersistentStore(store =>
			{
				store.UseSqlServer(SqlClientFactory.Instance, connection);
				store.ProvisionSchema();
			});
			q.AddJob<DataProcessingJob>(job => job.WithIdentity("native-processing").StoreDurably());
		});
		await using var provider = services.BuildServiceProvider();
		var manager = provider.GetRequiredService<IDataOrchestrationManager>();
		await manager.AddDataTaskForRecordTypeAsync("native", CancellationToken.None).ConfigureAwait(false);
		var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler().ConfigureAwait(false);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		try
		{
			await scheduler.Start(deadline.Token).ConfigureAwait(false);
			await scheduler.TriggerJob(new JobKey("native-plain"), cancellationToken: deadline.Token).ConfigureAwait(false);
			await scheduler.TriggerJob(new JobKey("native-context"), cancellationToken: deadline.Token).ConfigureAwait(false);
			await scheduler.TriggerJob(new JobKey("native-processing"), cancellationToken: deadline.Token).ConfigureAwait(false);
			var signals = provider.GetRequiredService<SmokeSignals>();
			await Task.WhenAll(signals.Plain.Task, signals.Context.Task, signals.Processing.Task).WaitAsync(deadline.Token).ConfigureAwait(false);
			var heartbeats = provider.GetRequiredService<JobHeartbeatTracker>();
			while (heartbeats.GetLastHeartbeat("native-processing") is null || heartbeats.GetLastHeartbeat("native-plain") is null)
			{
				await Task.Delay(20, deadline.Token).ConfigureAwait(false);
			}
		}
		finally { await scheduler.Shutdown(waitForJobsToComplete: true, CancellationToken.None).ConfigureAwait(false); }
		Console.WriteLine("NATIVE QUARTZ SQL: DataProcessingJob and both Excalibur adapters executed successfully.");
	}
}

internal sealed class SmokeSignals
{
	internal TaskCompletionSource Plain { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	internal TaskCompletionSource Context { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	internal TaskCompletionSource Processing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
internal sealed class SmokePlainJob(SmokeSignals signals) : IBackgroundJob
{
	public Task ExecuteAsync(CancellationToken cancellationToken) { signals.Plain.TrySetResult(); return Task.CompletedTask; }
}
internal sealed record SmokeContext(string Value);
internal sealed class SmokeContextJob(SmokeSignals signals) : IBackgroundJob<SmokeContext>
{
	public Task ExecuteAsync(SmokeContext context, CancellationToken cancellationToken)
	{
		if (context.Value != "persisted-context") { throw new InvalidOperationException("Persisted context was not restored."); }
		signals.Context.TrySetResult();
		return Task.CompletedTask;
	}
}
internal sealed class SmokeRegistry(SmokeSignals signals) : IDataProcessorRegistry
{
	public Func<IServiceProvider, IDataProcessor> GetFactory(string recordType) => _ => new SmokeProcessor(signals);
	public bool TryGetFactory(string recordType, out Func<IServiceProvider, IDataProcessor> factory)
	{
		factory = GetFactory(recordType);
		return recordType == "native";
	}
}
internal sealed class SmokeProcessor(SmokeSignals signals) : IDataProcessor
{
	public async Task<long> RunAsync(long completedCount, string? cursor, UpdateCompletedCount checkpoint, CancellationToken cancellationToken)
	{
		await checkpoint(completedCount + 1, "done", cancellationToken).ConfigureAwait(false);
		signals.Processing.TrySetResult();
		return completedCount + 1;
	}
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
[JsonSerializable(typeof(SmokeContext))]
internal sealed partial class SmokeJson : JsonSerializerContext;
