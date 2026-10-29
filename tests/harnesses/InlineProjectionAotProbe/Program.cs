using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InlineProjectionAotProbe;

internal static class Probe
{
    // The projection interfaces conservatively annotate all providers. This probe uses only the
    // explicitly typed store below and generated event metadata; it does not certify external providers.
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Known typed probe store and generated event serializer; no reflection serialization.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Known typed probe store and generated event serializer; no reflection serialization.")]
    public static async Task Main()
    {
        if (RuntimeFeature.IsDynamicCodeSupported) { throw new InvalidOperationException("Run the published native executable."); }
        var builder = Host.CreateApplicationBuilder();
        var store = new ProbeStore();
        builder.Services.AddSingleton<IProjectionStore<ReadModel>>(store);
        builder.Services.AllowInMemoryProjectionCheckpoints();
        builder.Services.Configure<GlobalStreamProjectionOptions>(o => o.SubscriptionScope = "native-inline-probe");
        builder.Services.AddAotEventSerializer(ProbeJson.Default);
        builder.Services.Configure<InMemoryEventStoreOptions>(o => o.EventTypeInfoResolver = ProbeJson.Default);
        builder.Services.AddExcalibur(x => x.AddEventSourcing(es =>
        {
            es.UseInMemory();
            es.RegisterEventTypes<Added>();
            es.AddRepository<Counter, Guid>(id => new Counter(id));
            es.AddProjection<ReadModel>(p => p.Inline().When<Added>((state, e) => state.Count += e.Amount));
        }));
        using var host = builder.Build();
        // Foreground save works without starting a worker or even starting the Host.
        var repository = host.Services.GetRequiredService<IEventSourcedRepository<Counter, Guid>>();
        var aggregate = new Counter(Guid.NewGuid());
        for (var i = 0; i < 20; i++) { aggregate.Add(); }
        var save = await repository.SaveAsync(aggregate, CancellationToken.None);
        if (save.Projections != ProjectionFreshness.Attested || save.SessionToken is not > 0 || store.Count != 20 || store.Writes != 1) { throw new InvalidOperationException("Native foreground fold failed."); }
        store.Fail = true;
        aggregate.Add();
        var pending = await repository.SaveAsync(aggregate, CancellationToken.None);
        if (pending.Projections != ProjectionFreshness.BoundExpired)
        { throw new InvalidOperationException("Expected a committed save whose read model is behind."); }
        if (pending.CommittedVersion != 21 || pending.SessionToken is null)
        { throw new InvalidOperationException("Lost commit evidence."); }
        store.Fail = false;
        // Automatic startup recovery, without issuing another save or explicitly driving a processor.
        await host.StartAsync();
        var reader = host.Services.GetRequiredService<IProjectionSessionReader>();
        var read = await reader.ReadAsync<ReadModel>(aggregate.Id.ToString(), pending, CancellationToken.None);
        if (read.Freshness != ProjectionFreshness.Fresh || read.Projection?.Count != 21 || store.Writes != 2)
        { throw new InvalidOperationException("Native recovery failed."); }
        await host.StopAsync();
        Console.WriteLine("PASS native Inline: 20-event single write, committed-but-behind reported on the result, automatic recovery, session read; no notification handlers.");
    }
}

[MessageName("InlineProbe.Added")]
public sealed record Added(int Amount) : DomainEvent;
public sealed class ReadModel { public int Count { get; set; } }
public sealed class Counter(Guid id) : AggregateRoot<Guid>(id)
{
    public void Add() => RaiseEvent(new Added(1));
    protected override bool ApplyEventInternal(IDomainEvent @event) => @event is Added;
}

[JsonSerializable(typeof(Added))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class ProbeJson : JsonSerializerContext;

// Purpose-built, clone-safe single-document storage. This isolates orchestration from provider AOT support.
internal sealed class ProbeStore : IProjectionStore<ReadModel>, IPositionedProjectionStore<ReadModel>, IProjectionDestinationIdentity
{
    private readonly object _gate = new();
    private long? _position;
    public int Count { get; private set; }
    public int Writes { get; private set; }
    public bool Fail { get; set; }
    public string Identity { get; } = "native-probe/" + Guid.NewGuid().ToString("N");
    public object? GetService(Type type) => type == typeof(IPositionedProjectionStore<ReadModel>) ? this
        : type == typeof(IProjectionDestinationIdentity) ? this
        : type == typeof(IProjectionTenantBoundary) ? ProjectionTenantBoundary.EstateWide : null;
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<ReadModel?> GetByIdAsync(string id, CancellationToken token)
    { lock (_gate) { return Task.FromResult(_position is null ? null : new ReadModel { Count = Count }); } }
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<(ReadModel? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id, CancellationToken token)
    { lock (_gate) { return Task.FromResult<(ReadModel?, ProjectionPosition)>((_position is null ? null : new ReadModel { Count = Count }, _position is { } p ? ProjectionPosition.At(p) : default)); } }
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(string id, ReadModel state, long? expectedPosition, long newPosition, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (Fail) { throw new IOException("Simulated destination outage after event commit."); }
            if (_position != expectedPosition) { return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Superseded, _position)); }
            Count = state.Count; _position = newPosition; Writes++;
            return Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));
        }
    }
    [RequiresDynamicCode("Matches provider interface; typed probe store.")]
    [RequiresUnreferencedCode("Matches provider interface; typed probe store.")]
    public Task UpsertAsync(string id, ReadModel projection, CancellationToken token) => throw new NotSupportedException();
    [RequiresDynamicCode("Matches provider interface; typed probe store.")]
    [RequiresUnreferencedCode("Matches provider interface; typed probe store.")]
    public Task UpsertUnnumberedAsync(string id, ReadModel projection, CancellationToken token) => throw new NotSupportedException();
    public Task DeleteAsync(string id, CancellationToken token) => throw new NotSupportedException();
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<IReadOnlyList<ReadModel>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken token) => throw new NotSupportedException();
    public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken token) => throw new NotSupportedException();
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<ProjectionRefoldResult> RefoldAtPositionAsync(string id, ReadModel projection, long atPosition, CancellationToken token) => throw new NotSupportedException();
    [RequiresDynamicCode("Matches provider interface; this probe implementation uses typed state only.")]
    [RequiresUnreferencedCode("Matches provider interface; this probe implementation uses typed state only.")]
    public Task<ProjectionRebuildResult> RebuildAtPositionAsync(string id, ReadModel projection, long newPosition, CancellationToken token) => throw new NotSupportedException();
}
