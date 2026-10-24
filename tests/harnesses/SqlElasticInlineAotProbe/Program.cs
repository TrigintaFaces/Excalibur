using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.SqlServer;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;

namespace SqlElasticInlineAotProbe;

internal static class Probe
{
    public static async Task Main(string[] args)
    {
        if (RuntimeFeature.IsDynamicCodeSupported) { throw new InvalidOperationException("Run the native executable."); }
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddAotEventSerializer(ProbeJson.Default);
        builder.Services.AddElasticSearchProjectionStore<ReadModel>(o =>
        {
            o.NodeUri = Environment.GetEnvironmentVariable("INLINE_ELASTIC_URI") ?? "http://127.0.0.1:19200";
            o.Index.IndexName = Environment.GetEnvironmentVariable("INLINE_INDEX") ?? "inline-native-probe";
            o.Index.EnsureSearchVisibilityOnProjectionCheckpoint = true;
            o.SourceTypeInfoResolver = ProbeJson.Default;
        });
        builder.Services.AddExcalibur(x => x.AddEventSourcing(es =>
        {
            es.UseSqlServer(sql => sql.ConnectionString(Environment.GetEnvironmentVariable("INLINE_SQL")
                ?? throw new InvalidOperationException("INLINE_SQL required")).EventTypeInfoResolver(ProbeJson.Default));
            es.RegisterEventTypes<Added>();
            es.AddRepository<Counter, Guid>(id => new Counter(id));
            es.AddProjection<ReadModel>(ProbeJson.Default.ReadModel, p => p.Inline().When<Added>((state, e) => state.Count += e.Amount));
        }));
        using var host = builder.Build();
        var mode = args.Length > 0 ? args[0] : "api";
        if (mode == "contracts")
        {
            await CheckContractsAsync(host.Services);
            return;
        }
        if (mode != "api" && mode != "pending" && mode != "verify") { await host.StartAsync(); }
        if (mode == "serve")
        {
            Console.WriteLine("READY native recovery host");
            await Task.Delay(TimeSpan.FromSeconds(180)); // delay-ok: bounded worker process lifetime; recovery assertions run independently in the driver.
            await host.StopAsync();
            return;
        }
        var repository = host.Services.GetRequiredService<IEventSourcedRepository<Counter, Guid>>();
        var id = args.Length > 1 ? Guid.Parse(args[1]) : Guid.NewGuid();
        var aggregate = await repository.GetByIdAsync(id, CancellationToken.None) ?? new Counter(id);
        var count = aggregate.Count;
        SaveResult save;
        if (mode is "recover" or "verify")
        {
            var head = await host.Services.GetRequiredService<IGlobalStreamQuery>().GetHeadPositionAsync(CancellationToken.None);
            // Synthesised from the stream head for the recovery probe: no save ran here, so no inline
            // wait was requested and NotRequested is the honest value to carry into the session read.
            save = new SaveResult(aggregate.Version, head, ProjectionFreshness.NotRequested);
        }
        else
        {
            for (var i = 0; i < 20; i++) { aggregate.Add(); }
            count += 20;
            save = await repository.SaveAsync(aggregate, CancellationToken.None);
            if (save.Projections == ProjectionFreshness.BoundExpired && mode == "pending")
            {
                if (save.SessionToken is not > 0) { throw new InvalidOperationException("Lost durable token."); }
                Console.WriteLine($"PENDING {id} count={count} position={save.SessionToken}");
                return;
            }
            if (mode == "pending") { throw new InvalidOperationException("Outage did not produce a behind read model."); }
        }
        var reader = host.Services.GetRequiredService<IProjectionSessionReader>();
        var result = await reader.ReadAsync<ReadModel>(aggregate.Id.ToString(), save, ProbeJson.Default.ReadModel, CancellationToken.None);
        if (save.SessionToken is not > 0 || result.Freshness != ProjectionFreshness.Fresh || result.Projection?.Count != count)
        { throw new InvalidOperationException("Native provider foreground/read failed."); }
        var reload = await repository.GetByIdAsync(id, CancellationToken.None);
        if (reload?.Count != count) { throw new InvalidOperationException("Native aggregate reload failed."); }
        Console.WriteLine($"PASS native SQL/Elastic {aggregate.Id} position={save.SessionToken}");
        await host.StopAsync();
    }

    private static async Task CheckContractsAsync(IServiceProvider services)
    {
        using (var connection = new SqlConnection())
        {
            var cause = new InvalidOperationException("native failure probe");
            try
            {
                await Excalibur.Data.DbConnectionExtensions.ResolveAsync(connection, new FailingRequest(cause));
                throw new InvalidOperationException("Request failure did not propagate.");
            }
            catch (Excalibur.Data.OperationFailedException error) when (ReferenceEquals(error.InnerException, cause))
            {
                if (error.Operation != typeof(List<int>).Name)
                { throw new InvalidOperationException("Native failure lost its result type."); }
            }
        }
        var registrations = new ServiceCollection();
        registrations.AddSqlServerSubscriptionCheckpointStore(() => new SqlConnection(Environment.GetEnvironmentVariable("INLINE_SQL")));
        await using var provider = registrations.BuildServiceProvider();
        var checkpoints = provider.GetRequiredService<ISubscriptionCheckpointStore>();
        var name = "native-contract-" + Guid.NewGuid();
        await checkpoints.BindSubscriptionAsync(name, new string('A', 64), CancellationToken.None);
        var first = (long)int.MaxValue + 100;
        var attempts = await Task.WhenAll(checkpoints.AdvanceCheckpointAsync(name, null, first, CancellationToken.None),
            checkpoints.AdvanceCheckpointAsync(name, null, first + 1, CancellationToken.None));
        if (attempts.Count(x => x == CheckpointAdvanceOutcome.Advanced) != 1) { throw new InvalidOperationException("Checkpoint insert race failed."); }
        var position = await checkpoints.GetCheckpointAsync(name, CancellationToken.None);
        if (await checkpoints.AdvanceCheckpointAsync(name, position, first + 2, CancellationToken.None) != CheckpointAdvanceOutcome.Advanced
            || await checkpoints.AdvanceCheckpointAsync(name, position, first + 3, CancellationToken.None) != CheckpointAdvanceOutcome.Superseded
            || !(await checkpoints.EnumerateCheckpointsAsync(CancellationToken.None)).Any(x => x.SubscriptionName == name && x.Position == first + 2))
        { throw new InvalidOperationException("Checkpoint CAS or enumeration failed."); }
        var store = services.GetRequiredService<IEventStore>();
        var snapshots = services.GetRequiredService<ISnapshotStore>();
        var snapshotId = Guid.NewGuid().ToString();
        var snapshot = new Snapshot
        {
            SnapshotId = Guid.NewGuid().ToString(), AggregateId = snapshotId, AggregateType = "Counter",
            Version = 3, Data = new byte[] { 1, 2, 3 }, CreatedAt = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object> { ["version"] = 7, ["custom"] = new SnapshotMetadata("native") },
        };
        await snapshots.SaveSnapshotAsync(snapshot, CancellationToken.None);
        var restored = await snapshots.GetLatestSnapshotAsync(snapshotId, "Counter", CancellationToken.None);
        if (restored?.Version != 3 || restored.Metadata?["version"] is not int schema || schema != 7
            || restored.Metadata["custom"] is not System.Text.Json.JsonElement custom || custom.GetProperty("label").GetString() != "native")
        { throw new InvalidOperationException("Native snapshot metadata mapping failed."); }
        var id = Guid.NewGuid().ToString();
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(5));
        var item = new Added(1) { OccurredAt = timestamp, Metadata = new Dictionary<string, object> { ["native"] = "metadata" } };
        var committed = await store.AppendAsync(id, "Counter", [item], -1, CancellationToken.None);
        var repeated = await store.AppendAsync(id, "Counter", [item], -1, CancellationToken.None);
        if (!committed.Success || repeated.Outcome != AppendOutcome.AlreadyCommitted
            || repeated.LastEventPosition != committed.LastEventPosition || repeated.NextExpectedVersion != 0)
        { throw new InvalidOperationException("Native committed-append reconciliation failed."); }
        var loaded = await store.LoadAsync(id, "Counter", CancellationToken.None);
        if (loaded.Count != 1 || loaded[0].Metadata is null || loaded[0].Timestamp != timestamp)
        { throw new InvalidOperationException("Native metadata/timestamp mapping failed."); }
        var rejected = await store.AppendAsync(id, "Counter", [new Added(1) { EventId = new string('X', 300) }], 0, CancellationToken.None);
        if (rejected.Success || (await store.LoadAsync(id, "Counter", CancellationToken.None)).Count != 1)
        { throw new InvalidOperationException("Native rollback/zero-match classification failed."); }
        Console.WriteLine("PASS native checkpoint CAS, enumeration, snapshot/custom metadata, timestamp and committed/rolled-back reconciliation");
    }
}

internal sealed class FailingRequest : Excalibur.Data.DataRequest<List<int>>
{
    public FailingRequest(Exception cause) => ResolveAsync = _ => Task.FromException<List<int>>(cause);
}

[MessageName("SqlElasticNative.Added")]
public sealed record Added(int Amount) : DomainEvent;
public sealed class ReadModel { public int Count { get; set; } }
public sealed record SnapshotMetadata([property: JsonPropertyName("label")] string Label);
public sealed class Counter(Guid id) : AggregateRoot<Guid>(id)
{
    public int Count { get; private set; }
    public void Add() => RaiseEvent(new Added(1));
    protected override bool ApplyEventInternal(IDomainEvent @event)
    {
        if (@event is not Added added) { return false; }
        Count += added.Amount;
        return true;
    }
}
[JsonSerializable(typeof(Added))]
[JsonSerializable(typeof(ReadModel))]
[JsonSerializable(typeof(SnapshotMetadata))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class ProbeJson : JsonSerializerContext;

