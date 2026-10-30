// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <inheritdoc />
internal sealed class ProjectionSessionReader : IProjectionSessionReader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISubscriptionCheckpointStore? _checkpointStore;
    private readonly IOptions<ProjectionSessionOptions> _options;
    private readonly IOptions<GlobalStreamProjectionOptions> _projectionOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IProjectionRegistry? _registry;
    private readonly InlineProjectionCoordinator? _inline;

    public ProjectionSessionReader(IServiceScopeFactory scopeFactory,
        IOptions<ProjectionSessionOptions> options, IOptions<GlobalStreamProjectionOptions> projectionOptions,
        ISubscriptionCheckpointStore? checkpointStore = null, ProjectionHealthState? healthState = null,
        TimeProvider? timeProvider = null, IProjectionRegistry? registry = null,
        InlineProjectionCoordinator? inline = null, ProjectionCapacity? capacity = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(projectionOptions);
        _scopeFactory = scopeFactory;
        _checkpointStore = checkpointStore is not null && capacity is not null
            ? new ProjectionCapacityCheckpointStore(checkpointStore, capacity, () => (checkpointStore as IProjectionDestinationScope)?.Scope)
            : checkpointStore;
        _options = options;
        _projectionOptions = projectionOptions;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _registry = registry;
        _inline = inline;
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Projection stores serialize the projection type reflectively.")]
    [RequiresDynamicCode("Projection stores serialize the projection type reflectively.")]
    public async ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(string projectionId,
        SaveResult session, CancellationToken cancellationToken) where TProjection : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = ProjectionStoreOperations<TProjection>.Legacy(scope.ServiceProvider.GetRequiredService<IProjectionStore<TProjection>>()).WithCapacity(scope.ServiceProvider);
        return await ReadCoreAsync(projectionId, session, store, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(string projectionId,
        SaveResult session, JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) where TProjection : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);
        ArgumentNullException.ThrowIfNull(typeInfo);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = ProjectionStoreOperations<TProjection>.Generated(scope.ServiceProvider, typeInfo);
        return await ReadCoreAsync(projectionId, session, store, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ProjectionRead<TProjection>> ReadCoreAsync<TProjection>(string projectionId,
        SaveResult session, ProjectionStoreOperations<TProjection> store, CancellationToken cancellationToken) where TProjection : class
    {
        if (_registry?.GetRegistration(typeof(TProjection))?.Mode == ProjectionMode.TransactionalInline)
        {
            throw new NotSupportedException("TransactionalInline projections have aggregate-local commit consistency, not global-prefix session tokens. Read through IProjectionStore after a successful coordinated SaveAsync.");
        }

        using var expiry = new CancellationTokenSource(_options.Value.Bound, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);
        var inline = _registry?.GetRegistration(typeof(TProjection))?.Mode == ProjectionMode.Inline;
        var key = SubscriptionKeys.ProjectionKey(typeof(TProjection));
        SubscriptionRoster? witness = null;
        if (session.SessionToken is { } token)
        {
            try
            {
                var destinationScope = await store.GetDestinationScopeAsync(linked.Token).ConfigureAwait(false);
                var fingerprint = inline && _inline is not null
                    ? await _inline.DestinationFingerprintAsync(typeof(TProjection), store.Destination, destinationScope, linked.Token).ConfigureAwait(false)
                    : SubscriptionKeys.DefinitionFingerprint(_projectionOptions.Value.SubscriptionScope,
                        _projectionOptions.Value.SubscriptionRevision, typeof(TProjection).Assembly.GetName().Name ?? string.Empty,
                        key, AsyncProjectionProcessingHost.DestinationDescriptor(ProjectionMode.Async, store.Destination, destinationScope) ?? string.Empty);
                witness = await AwaitWitnessAsync(inline, key, token, store.Destination, fingerprint, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
        }

        // Even after expiry return the available document, labelled as unconfirmed.
        var projection = await store.GetByIdAsync(projectionId, cancellationToken).ConfigureAwait(false);
        var freshness = session.SessionToken is null ? ProjectionFreshness.NotRequested : ProjectionFreshness.BoundExpired;
        if (witness is not null)
        {
            try
            {
                // Validate after the data read, including for saves without an Inline receipt.
                var current = await ReadRosterAsync(inline, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                var before = witness.Members.Single(m => m.ProjectionKey == key);
                var after = current?.Members.FirstOrDefault(m => m.ProjectionKey == key);
                if (current?.Identity == witness.Identity && after?.BindingId == before.BindingId
                    && after.Position >= session.SessionToken && after.Destination == store.Destination)
                {
                    freshness = ProjectionFreshness.Fresh;
                }
            }
            catch (OperationCanceledException) when (expiry.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
        }
        return new ProjectionRead<TProjection>(projection, freshness);
    }

    private async Task<SubscriptionRoster?> AwaitWitnessAsync(bool inline, string key, long position,
        string? destination, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destination)) { return null; }
        while (true)
        {
            var roster = await ReadRosterAsync(inline, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            var member = roster?.Members.FirstOrDefault(m => m.ProjectionKey == key);
            if (member is not null && member.DefinitionHash == fingerprint && member.BindingId != Guid.Empty && member.Position >= position
                && string.Equals(member.Destination, destination, StringComparison.Ordinal))
            {
                return roster;
            }
            await Task.Delay(_options.Value.PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<SubscriptionRoster?> ReadRosterAsync(bool inline, CancellationToken cancellationToken)
    {
        if (inline && _inline is not null)
        {
            return await _inline.ReadRosterAsync(cancellationToken).ConfigureAwait(false);
        }
        if (_checkpointStore is null) { return null; }
        var name = (inline ? "Inline:" : string.Empty) + _projectionOptions.Value.ProjectionName;
        return await _checkpointStore.ReadSubscriptionAsync(name, cancellationToken).ConfigureAwait(false);
    }
}
