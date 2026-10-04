// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
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
        Console.WriteLine("DataProcessing AOT verified: all configured values, both registration paths, empty-page traversal, handler scopes and checkpoints.");
    }

    private static async Task VerifyAsync(bool useBuilder)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Processing:SchemaName"] = "Smoke", ["Processing:TableName"] = "Tasks",
            ["Processing:QueueSize"] = "321", ["Processing:ProducerBatchSize"] = "12",
            ["Processing:ConsumerBatchSize"] = "3", ["Processing:MaxAttempts"] = "7",
            ["Processing:DispatcherTimeoutMilliseconds"] = "1234",
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
            options.DispatcherTimeoutMilliseconds != 1234)
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
