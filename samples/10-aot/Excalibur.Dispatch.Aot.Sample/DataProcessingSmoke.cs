// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
using Excalibur.EventSourcing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class DataProcessingSmoke
{
    public static async Task RunAsync()
    {
        await VerifyAsync(useBuilder: false).ConfigureAwait(false);
        await VerifyAsync(useBuilder: true).ConfigureAwait(false);
        await VerifyBulkAsync().ConfigureAwait(false);
        Console.WriteLine("DataProcessing AOT verified: all configured values, both registration paths, empty-page traversal, handler scopes and checkpoints.");
    }

    private static async Task VerifyBulkAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, SmokeLifetime>();
        services.AddSingleton<BulkProgress>();
        services.AddScoped<IProjectionProcessor, BulkProjectionProcessor>();
        services.AddDataProcessing(builder => builder.AddProcessor<BulkSmokeProcessor>()
            .AddRecordHandler<BulkSmokeHandler, int>());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<BulkSmokeProcessor>();
        var checkpoints = 0;
        var count = await processor.RunAsync(0, null, (_, _, _) =>
        {
            checkpoints++;
            return Task.CompletedTask;
        }, CancellationToken.None).ConfigureAwait(false);
        var progress = provider.GetRequiredService<BulkProgress>();
        if (count != 2 || checkpoints != 2 || progress.Handled != 2 || progress.Folds != 1 || InlineProjectionWait.IsSuppressed)
        {
            throw new InvalidOperationException("Native bulk processing did not preserve checkpoints and one final fold.");
        }
        Console.WriteLine("BulkDataProcessor AOT verified: sequential records, checkpoints, suppression and one final catch-up.");
    }

    private sealed class BulkProgress
    {
        public int Handled { get; set; }
        public int Folds { get; set; }
    }
    private sealed class BulkSmokeHandler(BulkProgress progress) : IRecordHandler<int>
    {
        public Task ProcessAsync(int record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!InlineProjectionWait.IsSuppressed || record != progress.Handled + 1)
            {
                throw new InvalidOperationException("Bulk records lost ordering or projection-wait suppression.");
            }
            progress.Handled++;
            return Task.CompletedTask;
        }
    }
    // A deterministic stand-in makes this smoke test independent of databases. Applications register
    // their actual projection processor and subscriptions; this stub performs no projection writes.
    private sealed class BulkProjectionProcessor(BulkProgress progress) : IProjectionProcessor
    {
        public Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InlineProjectionWait.IsSuppressed || progress.Handled != 2)
            {
                throw new InvalidOperationException("Bulk catch-up ran before records finished or under suppression.");
            }
            progress.Folds++;
            return Task.FromResult(new ProjectionProcessingResult(0, ProjectionProcessingOutcome.CaughtUp, ProjectionHaltReason.None));
        }
    }
    [DataTaskRecordType("BulkAotSmoke")]
    private sealed class BulkSmokeProcessor(IHostApplicationLifetime lifetime, IOptions<DataProcessingOptions> options,
        IServiceProvider services, ILogger<BulkSmokeProcessor> logger) : BulkDataProcessor<int>(lifetime, options, services, logger)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult(new CursorFetchResult<int>([1, 2], null));
    }

    private static async Task VerifyAsync(bool useBuilder)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Processing:SchemaName"] = "Smoke", ["Processing:TableName"] = "Tasks",
            ["Processing:QueueSize"] = "321", ["Processing:ProducerBatchSize"] = "12",
            ["Processing:ConsumerBatchSize"] = "3", ["Processing:MaxAttempts"] = "7",
            ["Processing:ProcessingStallTimeout"] = "00:00:01.234",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, SmokeLifetime>();
        services.AddSingleton<Progress>();
        services.AddScoped<HandlerLease>();
        if (useBuilder)
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDataProcessing(builder => builder
                .BindConfiguration("Processing")
                .AddProcessor<SmokeProcessor>()
                .AddRecordHandler<SmokeHandler, int>());
        }
        else
        {
            services.AddDataProcessor<SmokeProcessor>(configuration, "Processing");
            services.AddRecordHandler<SmokeHandler, int>();
        }
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        provider.GetRequiredService<IStartupValidator>().Validate();
        var options = provider.GetRequiredService<IOptions<DataProcessingOptions>>().Value;
        if (options.SchemaName != "Smoke" || options.TableName != "Tasks" || options.QueueSize != 321 ||
            options.ProducerBatchSize != 12 || options.ConsumerBatchSize != 3 || options.MaxAttempts != 7 ||
            options.ProcessingStallTimeout != TimeSpan.FromMilliseconds(1234))
        {
            throw new InvalidOperationException("Native configuration ignored an immutable DataProcessing option.");
        }
        await using var scope = provider.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<SmokeProcessor>();
        var checkpoints = 0;
        var completed = await processor.RunAsync(0, null, (_, _, _) =>
        {
            checkpoints++;
            return Task.CompletedTask;
        }, CancellationToken.None).ConfigureAwait(false);
        var progress = provider.GetRequiredService<Progress>();
        if (completed != 2 || checkpoints != 3 || progress.Handled != 2 || progress.DisposedScopes != 2)
        {
            throw new InvalidOperationException("Native DataProcessing execution or handler lifetime validation failed.");
        }
    }

    private sealed class Progress
    {
        public int Handled { get; set; }
        public int DisposedScopes { get; set; }
    }
    private sealed class HandlerLease(Progress progress) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            progress.DisposedScopes++;
            return ValueTask.CompletedTask;
        }
    }
    private sealed class SmokeHandler(Progress progress, HandlerLease lease) : IRecordHandler<int>
    {
        public Task ProcessAsync(int record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GC.KeepAlive(lease);
            progress.Handled++;
            return Task.CompletedTask;
        }
    }
    [DataTaskRecordType("AotSmoke")]
    private sealed class SmokeProcessor(IHostApplicationLifetime lifetime, IOptions<DataProcessingOptions> options,
        IServiceProvider services, ILogger<SmokeProcessor> logger) : DataProcessor<int>(lifetime, options, services, logger)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult(cursor is null ? new CursorFetchResult<int>([], "next") : new CursorFetchResult<int>([1, 2], null));
    }
    private sealed class SmokeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
