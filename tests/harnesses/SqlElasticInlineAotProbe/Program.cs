using System.Runtime.CompilerServices;
using System.Diagnostics.Metrics;
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
        using var capacityMonitor = new CapacityMonitor();
        var builder = Host.CreateApplicationBuilder();
        // Exercise all existing native scenarios at the tightest supported storage capacity.
        builder.Services.Configure<ProjectionCapacityOptions>(options =>
        {
            options.MaxConcurrentOperations = 1;
            options.MaxConcurrentReads = 1;
            options.MaxConcurrentWrites = 1;
        });
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
        builder.Services.AddInlineProjectionGenerationReader<ReadModel>("native-reader");
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
        capacityMonitor.AssertObserved();
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
        var binding = (await checkpoints.BindSubscriptionAsync(name, [new SubscriptionMember("Proj.A", new string('A', 64), "dest-a")], CancellationToken.None)).Members.Single().BindingId;
        var first = (long)int.MaxValue + 100;
        var attempts = await Task.WhenAll(checkpoints.AdvanceCheckpointAsync(name, "Proj.A", binding, null, first, CancellationToken.None),
            checkpoints.AdvanceCheckpointAsync(name, "Proj.A", binding, null, first + 1, CancellationToken.None));
        if (attempts.Count(x => x == CheckpointAdvanceOutcome.Advanced) != 1) { throw new InvalidOperationException("Checkpoint insert race failed."); }
        var position = await checkpoints.GetCheckpointAsync(name, "Proj.A", CancellationToken.None);
        if (await checkpoints.AdvanceCheckpointAsync(name, "Proj.A", binding, position, first + 2, CancellationToken.None) != CheckpointAdvanceOutcome.Advanced
            || await checkpoints.AdvanceCheckpointAsync(name, "Proj.A", binding, position, first + 3, CancellationToken.None) != CheckpointAdvanceOutcome.Superseded
            || !(await checkpoints.EnumerateCheckpointsAsync(CancellationToken.None)).Any(x => x.SubscriptionName == name && x.ProjectionKey == "Proj.A" && x.Position == first + 2))
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
        await CheckGenerationReaderAsync(services, checkpoints);
        await CheckRetirementAsync(services.GetRequiredService<IProjectionStore<ReadModel>>());
        Console.WriteLine("PASS native checkpoint CAS, enumeration, snapshot/custom metadata, timestamp and committed/rolled-back reconciliation");
    }
    private static async Task CheckRetirementAsync(IProjectionStore<ReadModel> store)
    {
        var token = CancellationToken.None;
        var aot = (IAotProjectionStore<ReadModel>)store.GetService(typeof(IAotProjectionStore<ReadModel>))!;
        var factory = (IAotProjectionGenerationReaderFactory<ReadModel>)store.GetService(typeof(IAotProjectionGenerationReaderFactory<ReadModel>))!;
        var identity = ((IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!).Identity;
        await using var reader = factory.OpenReader(identity, ProbeJson.Default.ReadModel);
        var visibility = (IProjectionVisibilityBarrier)store.GetService(typeof(IProjectionVisibilityBarrier))!;
        await visibility.EnsureVisibleAsync(token);
        var initialCount = await reader.CountAsync(null, token);
        var initialRows = (await reader.QueryAsync(null, new QueryOptions(Take: 1000), token)).Count;
        if (initialRows != initialCount) { throw new InvalidOperationException("Native retirement fixture exceeded its query window."); }
        var id = "retired-native-" + Guid.NewGuid();
        _ = await aot.UpsertAtPositionAsync(id, new ReadModel { Count = 1 }, null, 1, token);
        await visibility.EnsureVisibleAsync(token);
        if (await reader.CountAsync(null, token) != initialCount + 1) { throw new InvalidOperationException("Native retirement control was never visible."); }
        await store.DeleteAsync(id, token);
        await visibility.EnsureVisibleAsync(token);
        var read = await aot.GetWithPositionAsync(id, token);
        if (read.Projection is not null || read.Position.Kind != ProjectionPositionKind.Retired
            || await aot.GetByIdAsync(id, token) is not null
            || (await aot.UpsertAtPositionAsync(id, new ReadModel { Count = 2 }, null, 2, token)).Outcome != ProjectionAdvanceOutcome.Vanished
            || (await aot.RefoldAtPositionAsync(id, new ReadModel { Count = 2 }, 1, token)).Outcome != ProjectionRefoldOutcome.Vanished)
        { throw new InvalidOperationException("Native generated writes resurrected a retired key."); }
        if (await reader.CountAsync(null, token) != initialCount
            || (await reader.QueryAsync(null, new QueryOptions(Take: 1000), token)).Count != initialRows)
        { throw new InvalidOperationException("Native generated queries exposed retirement metadata."); }
        var refused = false;
        try { await aot.UpsertUnnumberedAsync(id, new ReadModel { Count = 2 }, token); }
        catch (InvalidOperationException) { refused = true; }
        if (!refused) { throw new InvalidOperationException("Native unnumbered write bypassed retirement."); }
        Console.WriteLine("PASS native generated retirement reads and all generated write surfaces");
    }

    private static async Task CheckGenerationReaderAsync(IServiceProvider services, ISubscriptionCheckpointStore checkpoints)
    {
        var token = CancellationToken.None;
        var repository = services.GetRequiredService<IEventSourcedRepository<Counter, Guid>>();
        var aggregate = new Counter(Guid.NewGuid());
        aggregate.Add();
        var save = await repository.SaveAsync(aggregate, token);
        var attestation = save.Attestation ?? throw new InvalidOperationException("Native generation setup was not attested.");
        var generations = (IProjectionGenerationStore)checkpoints;
        var key = SubscriptionKeys.ProjectionKey(typeof(ReadModel));
        const string readerName = "native-reader";
        await using var scope = services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<ProjectionGenerationControl<ReadModel>>();
        var previous = await control.ReadGenerationAsync(token);
        var switched = await control.CutOverAsync(key, previous?.Revision, 0, token);
        if (switched.Outcome != ProjectionCutoverOutcome.CutOver) { throw new InvalidOperationException("Native pointer initialization failed."); }
        var reader = scope.ServiceProvider.GetRequiredService<ProjectionGenerationReader<ReadModel>>();
        var actual = await reader.GetByIdAsync(aggregate.Id.ToString(), ProbeJson.Default.ReadModel, token);
        var rows = await reader.QueryAsync(new Dictionary<string, object> { ["Count"] = 1 }, null, ProbeJson.Default.ReadModel, token);
        var count = await reader.CountAsync(new Dictionary<string, object> { ["Count"] = 1 }, ProbeJson.Default.ReadModel, token);
        if (actual?.Count != 1 || rows.Count == 0 || count < 1) { throw new InvalidOperationException("Native Elasticsearch generation reads failed."); }
        var route = (await generations.ReadGenerationAsync(attestation.SubscriptionName, readerName, token))!;
        if (await checkpoints.ReleaseSubscriptionMemberAsync(attestation.SubscriptionName, key, route.Member.BindingId, token))
        { throw new InvalidOperationException("Native pointer did not pin its binding."); }
        if (!await generations.TryRemoveGenerationAsync(attestation.SubscriptionName, readerName, route.Revision, token))
        { throw new InvalidOperationException("Native reader decommission failed."); }
        Console.WriteLine("PASS native generated Elasticsearch generation get/query/count and durable reader pin");
        await CheckSqlGenerationReaderAsync(services);
    }

    private static async Task CheckSqlGenerationReaderAsync(IServiceProvider services)
    {
        var token = CancellationToken.None;
        var connectionString = Environment.GetEnvironmentVariable("INLINE_SQL")!;
        var tenant = services.GetRequiredService<ITenantContext>();
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID('dbo.NativeProjectionGenerationProbe', 'U') IS NULL
                    CREATE TABLE dbo.NativeProjectionGenerationProbe (Id nvarchar(128) NOT NULL, TenantId nvarchar(128) NOT NULL, Data nvarchar(max) NOT NULL, LastAppliedPosition bigint NULL, CreatedAt datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(), UpdatedAt datetimeoffset NOT NULL DEFAULT SYSDATETIMEOFFSET(), PRIMARY KEY(TenantId, Id));
                INSERT INTO dbo.NativeProjectionGenerationProbe (Id, TenantId, Data) VALUES (@Id, @Tenant, '{"count":7}');
                """;
            command.Parameters.AddWithValue("@Id", "native");
            command.Parameters.AddWithValue("@Tenant", tenant.TenantId!);
            _ = await command.ExecuteNonQueryAsync(token);
        }
        IProjectionStore<ReadModel> store = new SqlServerProjectionStore<ReadModel>(connectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SqlServerProjectionStore<ReadModel>>.Instance,
            tenant, "NativeWriterProjection");
        var factory = (IAotProjectionGenerationReaderFactory<ReadModel>)store.GetService(typeof(IAotProjectionGenerationReaderFactory<ReadModel>))!;
        await using var reader = factory.OpenReader("sqlserver/dbo/NativeProjectionGenerationProbe", ProbeJson.Default.ReadModel);
        if (string.IsNullOrWhiteSpace(await reader.GetDestinationScopeAsync(token)))
        { throw new InvalidOperationException("Native SQL generation reader lost its actual storage identity."); }
        var row = await reader.GetByIdAsync("native", token);
        var filters = new Dictionary<string, object> { ["count"] = 7 };
        var rows = await reader.QueryAsync(filters, null, token);
        var count = await reader.CountAsync(filters, token);
        if (row?.Count != 7 || rows.Count != 1 || count != 1) { throw new InvalidOperationException("Native SQL exact-generation reads failed."); }
        IProjectionStore<ReadModel> retiringStore = new SqlServerProjectionStore<ReadModel>(connectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SqlServerProjectionStore<ReadModel>>.Instance,
            tenant, "NativeProjectionGenerationProbe");
        await retiringStore.DeleteAsync("native", token);
        if (await reader.GetByIdAsync("native", token) is not null
            || (await reader.QueryAsync(null, null, token)).Count != 0
            || await reader.CountAsync(null, token) != 0)
        { throw new InvalidOperationException("Native SQL generation reads exposed a retired marker."); }
        Console.WriteLine("PASS native generated SQL generation get/query/count on an exact destination independent of the writer");
    }

}

internal sealed class CapacityMonitor : IDisposable
{
    private readonly MeterListener _listener = new();
    private long _active;
    private long _peak;
    private long _observed;

    internal CapacityMonitor()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Excalibur.EventSourcing.ProjectionCapacity"
                && instrument.Name == "projection.capacity.active") { listener.EnableMeasurementEvents(instrument); }
        };
        _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "operation" && tag.Value is "rebuild") { return; }
            }
            if (value > 0) { Interlocked.Increment(ref _observed); }
            var active = Interlocked.Add(ref _active, value);
            long previous;
            do { previous = Volatile.Read(ref _peak); if (previous >= active) { break; } }
            while (Interlocked.CompareExchange(ref _peak, active, previous) != previous);
        });
        _listener.Start();
    }

    internal void AssertObserved()
    {
        if (Volatile.Read(ref _observed) == 0 || Volatile.Read(ref _peak) != 1)
        {
            throw new InvalidOperationException("Native storage admission was not observed or exceeded the configured application capacity of one.");
        }
        Console.WriteLine("PASS native shared storage admission peak=1");
    }

    public void Dispose() => _listener.Dispose();
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

