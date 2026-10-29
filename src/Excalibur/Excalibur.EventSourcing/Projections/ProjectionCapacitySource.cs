// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

internal static class ProjectionCapacitySource
{
    internal static ValueTask<T> ReadAsync<T>(IServiceProvider services, IGlobalStreamQuery query,
        Func<CancellationToken, ValueTask<T>> operation, CancellationToken token) =>
        services.GetService<ProjectionCapacity>() is { } capacity
            ? capacity.ExecuteAsync((query as IProjectionDestinationScope)?.Scope, ProjectionCapacityKind.Read, operation, token)
            : operation(token);

    internal static async Task WriteAsync(IServiceProvider services, object store,
        Func<CancellationToken, Task> operation, CancellationToken token)
    {
        if (services.GetService<ProjectionCapacity>() is not { } capacity)
        {
            await operation(token).ConfigureAwait(false);
            return;
        }
        _ = await capacity.ExecuteAsync((store as IProjectionDestinationScope)?.Scope,
            ProjectionCapacityKind.Write, async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return true;
            }, token).ConfigureAwait(false);
    }

    internal static ISubscriptionCheckpointStore Checkpoints(IServiceProvider services, ISubscriptionCheckpointStore store) =>
        services.GetService<ProjectionCapacity>() is { } capacity
            ? new ProjectionCapacityCheckpointStore(store, capacity,
                () => (store as IProjectionDestinationScope)?.Scope)
            : store;
}
