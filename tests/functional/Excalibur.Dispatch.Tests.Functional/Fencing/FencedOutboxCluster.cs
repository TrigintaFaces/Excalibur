// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Functional.Fencing;

/// <summary>
///     A two-node outbox cluster, in-process and deterministic: two independently-composed containers over one
///     shared outbox store and one shared leader-election state.
/// </summary>
/// <remarks>
///     <para>
///     Each node is composed through the public registration a consumer writes —
///     <c>AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.UseInMemory()).AddLeaderElection(le =&gt; le.UseInMemory()))</c>
///     — and nothing about fencing is configured. That is the point: fencing is default-on once a leader
///     election is registered, so a node that never mentions it must still be fenced, and an arm that had to
///     opt into fencing would not be testing what a consumer gets.
///     </para>
///     <para>
///     <b>Two things are shared, and both correspond to real infrastructure.</b> The
///     <see cref="InMemoryLeaderElectionSharedState"/> stands for the coordination store the nodes contend
///     through (and carries the monotonic fencing-token counter). The <see cref="IOutboxStore"/> instance
///     stands for the one durable outbox both nodes drain — it is registered last in each container, which
///     wins over the provider's own registration, so both nodes genuinely address the same rows. Everything
///     else is per-node, as it is per-process in a real deployment.
///     </para>
///     <para>
///     A fresh shared state is created per cluster rather than using
///     <see cref="InMemoryLeaderElectionSharedState.Default"/>, so concurrently-running arms cannot contend
///     for the same resource name or observe each other's fencing tokens.
///     </para>
/// </remarks>
internal sealed class FencedOutboxCluster : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _ownedNodes = [];
    private readonly InMemoryLeaderElectionSharedState _sharedState = new();

    private FencedOutboxCluster()
    {
    }

    /// <summary>Gets the transport both nodes publish through; records every delivery for assertion.</summary>
    public RecordingMessageBusAdapter Transport { get; } = new();

    /// <summary>Gets the single shared outbox store both nodes drain.</summary>
    public IOutboxStore Store { get; private set; } = null!;

    /// <summary>Gets node A — the first to be elected, and the one superseded by the handover.</summary>
    public FencedOutboxNode NodeA { get; private set; } = null!;

    /// <summary>Gets node B — elected by <see cref="HandOverLeadershipToNodeBAsync"/>.</summary>
    public FencedOutboxNode NodeB { get; private set; } = null!;

    /// <summary>Gets the fencing token node A held before the handover, captured while it was still leader.</summary>
    public long TokenHeldByNodeABeforeHandover { get; private set; }

    /// <summary>
    ///     Builds both nodes and elects node A, so the cluster is handed back in the state a real deployment
    ///     reaches on start: one leader holding a minted token, one follower.
    /// </summary>
    /// <returns>The started cluster.</returns>
    public static async Task<FencedOutboxCluster> StartAsync()
    {
        var cluster = new FencedOutboxCluster();

        // Node A is built first and owns the store the whole cluster shares.
        cluster.NodeA = cluster.BuildNode("node-a", sharedStore: null);
        cluster.Store = cluster.NodeA.Store;
        cluster.NodeB = cluster.BuildNode("node-b", cluster.Store);

        await cluster.NodeA.Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
        cluster.TokenHeldByNodeABeforeHandover = cluster.NodeA.Gate.FencingToken
            ?? throw new InvalidOperationException(
                "Node A was started but minted no fencing token. The cluster cannot demonstrate fencing from "
                + "a state that is not fenced, so this is a harness failure, not a test failure.");

        return cluster;
    }

    /// <summary>
    ///     Moves leadership from node A to node B the way a real handover happens: A relinquishes, B acquires
    ///     and mints a strictly higher token.
    /// </summary>
    public async Task HandOverLeadershipToNodeBAsync()
    {
        await NodeA.Election.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await NodeB.Election.StartAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a node that believes it still holds the tenure identified by <paramref name="pinnedToken"/> —
    ///     a node partitioned away from the coordination store whose lease has not yet expired locally.
    /// </summary>
    /// <param name="pinnedToken">The tenure the node still believes it holds.</param>
    /// <returns>The partitioned node, disposed with the cluster.</returns>
    /// <remarks>
    ///     The pinned gate is the node's own belief about its leadership, which is the one thing that cannot
    ///     be obtained from the real election — the real election is correct, and a correct election is
    ///     precisely what a partitioned node does not have. Everything downstream of the gate is shipped code.
    /// </remarks>
    public async Task<FencedOutboxNode> AddPartitionedNodeAsync(long pinnedToken)
    {
        var node = BuildNode(
            "node-partitioned",
            Store,
            pinnedGate: new PinnedLeadershipGate(pinnedToken));

        _ownedNodes.Add(node);
        await Task.CompletedTask.ConfigureAwait(false);
        return node;
    }

    /// <summary>
    ///     Reads the shared store's own counts without disturbing it.
    /// </summary>
    /// <returns>The store's current statistics.</returns>
    /// <remarks>
    ///     Deliberately NOT <see cref="IOutboxStore.GetUnsentMessagesAsync(int, CancellationToken)"/>: that
    ///     member is a <em>claim</em>, not a read — it leases the rows it returns, so using it to "look at"
    ///     the outbox silently takes the very rows the next assertion expects the live leader to drain. The
    ///     admin statistics are the observation-only surface.
    /// </remarks>
    public async Task<OutboxStatistics> StatisticsAsync()
    {
        var admin = Store.GetService(typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin
            ?? throw new InvalidOperationException(
                "The shared outbox store does not expose the admin surface, so the arms cannot observe it "
                + "without claiming rows.");

        return await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var node in _ownedNodes)
        {
            await node.DisposeAsync().ConfigureAwait(false);
        }

        if (NodeB is not null)
        {
            await NodeB.DisposeAsync().ConfigureAwait(false);
        }

        if (NodeA is not null)
        {
            await NodeA.DisposeAsync().ConfigureAwait(false);
        }
    }

    private FencedOutboxNode BuildNode(
        string instanceId,
        IOutboxStore? sharedStore,
        ILeaderProcessingGate? pinnedGate = null)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(static b => b.SetMinimumLevel(LogLevel.Warning));

        // Per-cluster election state rather than the process-wide default, so arms running side by side
        // neither contend for the resource name nor share a fencing-token counter.
        _ = services.AddSingleton(_sharedState);

        // The consumer registration. Nothing here mentions fencing: registering a leader election is the
        // multi-instance signal, and the outbox is fenced by default from that alone.
        _ = services.AddExcalibur(x =>
        {
            _ = x.AddOutbox(static o => o.UseInMemory());
            _ = x.AddLeaderElection(le => le
                .UseInMemory()
                .WithOptions(o => o.InstanceId = instanceId));
        });

        // The transport a consumer supplies. Recording, so the arms can assert what actually reached a
        // transport rather than inferring delivery from the store's own bookkeeping.
        _ = services.AddSingleton<IMessageBusAdapter>(Transport);

        if (pinnedGate is not null)
        {
            // Replaces the real gate: this node's (wrong) belief about its own leadership.
            _ = services.AddSingleton(pinnedGate);
        }

        // Registered last so it wins over the provider's own store registration — this is what makes the
        // nodes address one outbox instead of one each.
        if (sharedStore is not null)
        {
            _ = services.AddSingleton(sharedStore);
        }

        // The payload serializer the publisher serializes outbound bodies with. Choosing one is the
        // consumer's call — the framework registers the registry but deliberately sets no current
        // serializer — so this is the shipped registration for "use System.Text.Json".
        _ = services.AddPluggableSerializer(
            SerializerIds.SystemTextJson,
            new SystemTextJsonSerializer(),
            setAsCurrent: true);

        // Constructed by an explicit factory rather than by implementation type: the publisher declares two
        // 5-parameter constructors (single-adapter and multi-transport) and DI cannot choose between them.
        // The single-adapter one is the shape this cluster composes.
        _ = services.AddSingleton<IOutboxPublisher>(sp => new MessageBusOutboxPublisher(
            sp.GetRequiredService<IOutboxStore>(),
            sp.GetRequiredService<IPayloadSerializer>(),
            sp.GetRequiredService<IMessageBusAdapter>(),
            sp,
            sp.GetRequiredService<ILogger<MessageBusOutboxPublisher>>()));

        var provider = services.BuildServiceProvider();

        return new FencedOutboxNode(
            provider,
            provider.GetRequiredService<IOutboxPublisher>(),
            provider.GetRequiredService<IOutboxStore>(),
            provider.GetRequiredService<ILeaderElection>(),
            provider.GetRequiredService<ILeaderProcessingGate>());
    }
}

/// <summary>One composed node of a <see cref="FencedOutboxCluster"/>.</summary>
/// <param name="Provider">The node's own container.</param>
/// <param name="Publisher">The node's drain — the consumer-facing entry point the arms exercise.</param>
/// <param name="Store">The outbox store the node addresses.</param>
/// <param name="Election">The node's leader election.</param>
/// <param name="Gate">The node's view of its own leadership, including the fencing token it would present.</param>
internal sealed record FencedOutboxNode(
    ServiceProvider Provider,
    IOutboxPublisher Publisher,
    IOutboxStore Store,
    ILeaderElection Election,
    ILeaderProcessingGate Gate) : IAsyncDisposable
{
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Election.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // An election that never started has nothing to relinquish; a teardown failure must not mask the
            // arm's own result.
        }

        await Provider.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
///     A leadership gate pinned to one tenure: it reports that this node leads and presents
///     <paramref name="fencingToken"/>, regardless of what the coordination store says.
/// </summary>
/// <param name="fencingToken">The tenure this node believes it still holds.</param>
/// <remarks>
///     This is a partitioned node's own belief about its leadership. It is deliberately NOT a fake store and
///     NOT a fake publisher: the refusal being asserted comes from the real store's high-water comparison,
///     reached through the real publisher's real capability probe.
/// </remarks>
internal sealed class PinnedLeadershipGate(long fencingToken) : ILeaderProcessingGate
{
    /// <inheritdoc/>
    public bool ShouldProcess => true;

    /// <inheritdoc/>
    public long? FencingToken => fencingToken;
}

/// <summary>
///     A transport adapter that records the identity of every message handed to it.
/// </summary>
/// <remarks>
///     Delivery is asserted from this record rather than from the outbox's own status column, because the
///     duplicate-delivery failure fencing prevents is precisely one where the store's bookkeeping looks
///     correct and a message went out twice.
/// </remarks>
internal sealed class RecordingMessageBusAdapter : IMessageBusAdapter
{
    private readonly ConcurrentQueue<string> _published = new();

    /// <summary>Gets the message ids handed to this transport, in order.</summary>
    public IReadOnlyList<string> Published => [.. _published];

    /// <inheritdoc/>
    public string Name => "recording";

    /// <inheritdoc/>
    public bool IsConnected => true;

    /// <inheritdoc/>
    public bool SupportsPublishing => true;

    /// <inheritdoc/>
    public bool SupportsSubscription => false;

    /// <inheritdoc/>
    public bool SupportsTransactions => false;

    /// <inheritdoc/>
    public Task<IMessageResult> PublishAsync(
        IDispatchMessage message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The publisher rebuilds the context with MessageId == the outbound row's id, so this records exactly
        // which outbox row reached a transport.
        _published.Enqueue(context.MessageId ?? string.Empty);
        return Task.FromResult(MessageResult.Success());
    }

    /// <inheritdoc/>
    public Task SubscribeAsync(
        string subscriptionName,
        Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler,
        MessageBusOptions? options,
        CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task InitializeAsync(MessageBusOptions options, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken) =>
        Task.FromResult(HealthCheckResult.Healthy());

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A message staged into the outbox by the fencing arms.</summary>
internal sealed class WidgetShipped
{
    /// <summary>Gets or sets the widget identifier.</summary>
    public string WidgetId { get; set; } = string.Empty;
}
