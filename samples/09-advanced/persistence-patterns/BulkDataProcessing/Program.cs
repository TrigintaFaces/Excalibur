// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Data.DataProcessing;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.InMemory;
using Excalibur.Domain.Model;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

await Example.RunAsync(bulk: false);
await Example.RunAsync(bulk: true);

internal static class Example
{
    public static async Task RunAsync(bool bulk)
    {
        var progress = new RealProgress { Bulk = bulk };
        var services = ConfigureServices(progress);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        IDataProcessor processor = bulk ? scope.ServiceProvider.GetRequiredService<RealBulkProcessor>()
            : scope.ServiceProvider.GetRequiredService<RealRegularProcessor>();
        var store = provider.GetRequiredService<IProjectionStore<BackfillAccountProjection>>();
        var events = provider.GetRequiredService<IEventStore>();
        var checkpoints = 0;
        var count = await processor.RunAsync(0, null, async (completed, _, token) =>
        {
            checkpoints++;
            if (completed != checkpoints || progress.Saved != checkpoints || progress.Projected != (bulk ? 0 : checkpoints))
            {
                throw new InvalidOperationException($"Mode={bulk}: completed={completed}, checkpoints={checkpoints}, saved={progress.Saved}, projected={progress.Projected}.");
            }
            var persisted = await store.CountAsync(null, token).ConfigureAwait(false);
            var savedEvents = await events.LoadAsync($"backfill-{completed}", new Account().AggregateType, token).ConfigureAwait(false);
            if (persisted != (bulk ? 0 : checkpoints) || savedEvents.Count != 1)
            {
                throw new InvalidOperationException("Persisted events or projections did not match the checkpoint boundary.");
            }
        }, CancellationToken.None).ConfigureAwait(false);
        var projected = await store.CountAsync(null, CancellationToken.None).ConfigureAwait(false);
        if (count != 2 || checkpoints != 2 || progress.Projected != 2 || projected != 2)
        {
            throw new InvalidOperationException("Real projection catch-up failed to persist both account projections.");
        }
        for (var record = 1; record <= 2; record++)
        {
            var state = await store.GetByIdAsync($"backfill-{record}", CancellationToken.None).ConfigureAwait(false);
            if (state?.Balance != record) throw new InvalidOperationException("Persisted projection contents are incorrect.");
        }
        Console.WriteLine($"DataProcessing real inline projection verified: bulk={bulk}, saves=2, checkpoints=2, persisted projections=2.");
    }

    private static ServiceCollection ConfigureServices(RealProgress progress)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole());
        services.AddMetrics();
        services.AddSingleton(progress);
        services.AddSingleton<IHostApplicationLifetime, SmokeLifetime>();
        services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
        {
            builder.RegisterEventTypes<AccountOpened>();
            builder.EnableProjectionProcessing(options => options.SubscriptionScope = "bulk-sample/memory-events/memory-views");
            builder.AddRepository<Account>(_ => new Account());
            builder.AddProjection<BackfillAccountProjection>(projection => projection.Inline()
                .When<AccountOpened>((state, e, _) =>
                {
                    state.Balance = e.InitialDeposit;
                    progress.Projected++;
                }));
        }));
        services.AddInMemoryEventStore();
        services.AddInMemoryProjectionStore<BackfillAccountProjection>();
        services.AddDataProcessing(builder =>
        {
            if (progress.Bulk) builder.AddProcessor<RealBulkProcessor>();
            else builder.AddProcessor<RealRegularProcessor>();
            builder.AddRecordHandler<RealHandler, int>();
        });
        return services;
    }

    internal sealed class BackfillAccountProjection
    {
        public decimal Balance { get; set; }
    }
    private sealed class RealProgress
    {
        public bool Bulk { get; init; }
        public int Saved { get; set; }
        public int Projected { get; set; }
    }
    private sealed class RealHandler(IEventSourcedRepository<Account> repository, RealProgress progress) : IRecordHandler<int>
    {
        public async Task ProcessAsync(int record, CancellationToken cancellationToken)
        {
            if (record != progress.Saved + 1) throw new InvalidOperationException("Record order changed or handlers overlapped.");
            var account = new Account();
            account.Open($"backfill-{record}", record);
            var saved = await repository.SaveAsync(account, cancellationToken).ConfigureAwait(false);
            if (saved.CommittedVersion != 1 || (progress.Bulk && saved.Projections != ProjectionFreshness.Deferred))
            {
                throw new InvalidOperationException("Repository did not commit the event with the expected projection policy.");
            }
            progress.Saved++;
        }
    }
    [DataTaskRecordType("RealBulkSample")]
    private sealed class RealBulkProcessor(IHostApplicationLifetime lifetime, IOptions<DataProcessingOptions> options,
        IServiceProvider services, ILogger<RealBulkProcessor> logger) : BulkDataProcessor<int>(lifetime, options, services, logger)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult(new CursorFetchResult<int>([1, 2], null));
    }
    [DataTaskRecordType("RealRegularSample")]
    private sealed class RealRegularProcessor(IHostApplicationLifetime lifetime, IOptions<DataProcessingOptions> options,
        IServiceProvider services, ILogger<RealRegularProcessor> logger) : DataProcessor<int>(lifetime, options, services, logger)
    {
        public override Task<CursorFetchResult<int>> FetchBatchAsync(string? cursor, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult(new CursorFetchResult<int>([1, 2], null));
    }

    private sealed class SmokeLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}

[MessageName("BulkDataProcessing.AccountOpened.v1")]
internal sealed record AccountOpened : DomainEvent
{
    public required string AccountId { get; init; }
    public required decimal InitialDeposit { get; init; }
}

internal sealed class Account : AggregateRoot
{
    public void Open(string id, decimal deposit) =>
        RaiseEvent(new AccountOpened { AccountId = id, InitialDeposit = deposit });

    protected override bool ApplyEventInternal(IDomainEvent @event)
    {
        if (@event is not AccountOpened opened) return false;
        Id = opened.AccountId;
        return true;
    }
}
