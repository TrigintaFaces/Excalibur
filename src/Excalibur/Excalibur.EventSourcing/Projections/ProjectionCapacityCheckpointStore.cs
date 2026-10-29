// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>A local execution view; never replaces the provider registration or hides its other capabilities.</summary>
internal sealed class ProjectionCapacityCheckpointStore(
    ISubscriptionCheckpointStore inner, ProjectionCapacity capacity, Func<string?> resource) : ISubscriptionCheckpointStore
{
    public Task<SubscriptionRoster> BindSubscriptionAsync(string subscriptionName, IReadOnlyList<SubscriptionMember> members, CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Write, token => inner.BindSubscriptionAsync(subscriptionName, members, token), cancellationToken);

    public Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Read, token => inner.ReadSubscriptionAsync(subscriptionName, token), cancellationToken);

    public Task<bool> ReleaseSubscriptionMemberAsync(string subscriptionName, string projectionKey, Guid bindingId, CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Write, token => inner.ReleaseSubscriptionMemberAsync(subscriptionName, projectionKey, bindingId, token), cancellationToken);

    public Task<long?> GetCheckpointAsync(string subscriptionName, string projectionKey, CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Read, token => inner.GetCheckpointAsync(subscriptionName, projectionKey, token), cancellationToken);

    public Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(string subscriptionName, string projectionKey, Guid bindingId,
        long? expectedPosition, long newPosition, CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Write, token => inner.AdvanceCheckpointAsync(subscriptionName, projectionKey, bindingId, expectedPosition, newPosition, token), cancellationToken);

    public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken) =>
        Run(ProjectionCapacityKind.Read, inner.EnumerateCheckpointsAsync, cancellationToken);

    private Task<T> Run<T>(ProjectionCapacityKind kind, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        capacity.ExecuteAsync(resource(), kind, token => new ValueTask<T>(operation(token)), cancellationToken).AsTask();
}
