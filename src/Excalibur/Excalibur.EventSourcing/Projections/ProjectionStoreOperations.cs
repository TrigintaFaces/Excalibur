// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

/// <summary>Closes persistence operations before entering the shared folding algorithms.</summary>
internal sealed class ProjectionStoreOperations<TProjection> where TProjection : class
{
    private readonly IProjectionStore<TProjection> _store;
    private ProjectionStoreOperations(IProjectionStore<TProjection> store) => _store = store;

    internal required Func<string, CancellationToken, Task<TProjection?>> GetByIdAsync { get; init; }
    internal required Func<string, TProjection, CancellationToken, Task> UpsertAsync { get; init; }
    internal required Func<string, CancellationToken, Task<(TProjection? Projection, ProjectionPosition Position)>> GetWithPositionAsync { get; init; }
    internal required Func<string, TProjection, long?, long, CancellationToken, Task<ProjectionAdvanceResult>> UpsertAtPositionAsync { get; init; }
    internal required Func<string, TProjection, long, CancellationToken, Task<ProjectionRefoldResult>> RefoldAtPositionAsync { get; init; }
    internal required Func<string, TProjection, CancellationToken, Task> UpsertUnnumberedAsync { get; init; }
    internal bool IsPositioned { get; private init; }

    internal ProjectionStoreOperations<TProjection> ResolveRequired(IReadOnlyList<ProjectionEvent> events)
    {
        _ = PositionedProjectionWriter<TProjection>.ResolveRequired(_store, events);
        return this;
    }

    [RequiresUnreferencedCode("Legacy projection stores may serialize reflectively.")]
    [RequiresDynamicCode("Legacy projection stores may serialize reflectively.")]
    internal static ProjectionStoreOperations<TProjection> Legacy(IProjectionStore<TProjection> store)
    {
        return Legacy(store, PositionedProjectionWriter<TProjection>.Resolve(store));
    }

    [RequiresUnreferencedCode("Legacy projection stores may serialize reflectively.")]
    [RequiresDynamicCode("Legacy projection stores may serialize reflectively.")]
    internal static ProjectionStoreOperations<TProjection> Legacy(IProjectionStore<TProjection> store, IPositionedProjectionStore<TProjection>? positioned)
    {
        return new(store)
        {
            GetByIdAsync = store.GetByIdAsync,
            UpsertAsync = store.UpsertAsync,
            GetWithPositionAsync = positioned is null ? MissingRead : positioned.GetWithPositionAsync,
            UpsertAtPositionAsync = positioned is null ? MissingAdvance : positioned.UpsertAtPositionAsync,
            RefoldAtPositionAsync = positioned is null ? MissingRefold : positioned.RefoldAtPositionAsync,
            UpsertUnnumberedAsync = positioned is null ? store.UpsertAsync : positioned.UpsertUnnumberedAsync,
            IsPositioned = positioned is not null,
        };
    }

    internal static ProjectionStoreOperations<TProjection> Generated(IServiceProvider services, JsonTypeInfo<TProjection> typeInfo)
    {
        var store = services.GetRequiredService<IProjectionStore<TProjection>>();
        var capability = store.GetService(typeof(IAotProjectionStore<TProjection>)) as IAotProjectionStore<TProjection>
            ?? throw new InvalidOperationException($"Projection store for '{typeof(TProjection).Name}' does not provide generated-metadata operations through its outer decorator. Configure a supported store with the same generated JSON context; reflection fallback is not allowed.");
        if (!ReferenceEquals(capability.TypeInfo, typeInfo))
        {
            throw new InvalidOperationException($"Projection '{typeof(TProjection).Name}' must use the same generated JsonTypeInfo instance as its configured source serializer.");
        }

        return new(store)
        {
            GetByIdAsync = capability.GetByIdAsync,
            UpsertAsync = static (_, _, _) => throw new InvalidOperationException("Generated projection processing requires positioned writes."),
            GetWithPositionAsync = capability.GetWithPositionAsync,
            UpsertAtPositionAsync = capability.UpsertAtPositionAsync,
            RefoldAtPositionAsync = capability.RefoldAtPositionAsync,
            UpsertUnnumberedAsync = capability.UpsertUnnumberedAsync,
            IsPositioned = true,
        };
    }

    private static Task<(TProjection?, ProjectionPosition)> MissingRead(string id, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
    private static Task<ProjectionAdvanceResult> MissingAdvance(string id, TProjection state, long? expected, long next, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
    private static Task<ProjectionRefoldResult> MissingRefold(string id, TProjection state, long position, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
}
