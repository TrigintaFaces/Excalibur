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
    private ProjectionCapacity? _capacity;
    private ProjectionTenantBoundary? _boundary;
    private ProjectionStoreOperations(IProjectionStore<TProjection> store) => _store = store;

    private Func<string, CancellationToken, Task<TProjection?>> _getByIdAsync = null!;
    internal required Func<string, CancellationToken, Task<TProjection?>> GetByIdAsync { get { VerifyBoundary(); return _getByIdAsync; } init => _getByIdAsync = value; }
    private Func<string, TProjection, CancellationToken, Task> _upsertAsync = null!;
    internal required Func<string, TProjection, CancellationToken, Task> UpsertAsync { get { VerifyBoundary(); return _upsertAsync; } init => _upsertAsync = value; }
    private Func<string, CancellationToken, Task<(TProjection? Projection, ProjectionPosition Position)>> _getWithPositionAsync = null!;
    internal required Func<string, CancellationToken, Task<(TProjection? Projection, ProjectionPosition Position)>> GetWithPositionAsync { get { VerifyBoundary(); return _getWithPositionAsync; } init => _getWithPositionAsync = value; }
    private Func<string, TProjection, long?, long, CancellationToken, Task<ProjectionAdvanceResult>> _upsertAtPositionAsync = null!;
    internal required Func<string, TProjection, long?, long, CancellationToken, Task<ProjectionAdvanceResult>> UpsertAtPositionAsync { get { VerifyBoundary(); return _upsertAtPositionAsync; } init => _upsertAtPositionAsync = value; }
    private Func<string, TProjection, long, CancellationToken, Task<ProjectionRefoldResult>> _refoldAtPositionAsync = null!;
    internal required Func<string, TProjection, long, CancellationToken, Task<ProjectionRefoldResult>> RefoldAtPositionAsync { get { VerifyBoundary(); return _refoldAtPositionAsync; } init => _refoldAtPositionAsync = value; }
    private Func<string, TProjection, CancellationToken, Task> _upsertUnnumberedAsync = null!;
    internal required Func<string, TProjection, CancellationToken, Task> UpsertUnnumberedAsync { get { VerifyBoundary(); return _upsertUnnumberedAsync; } init => _upsertUnnumberedAsync = value; }
    internal string? DestinationScope => ProjectionTenantValidation.Scope(CaptureBoundary(),
        (_store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope);
    internal async ValueTask<string?> GetDestinationScopeAsync(CancellationToken cancellationToken)
    {
        var boundary = CaptureBoundary();
        var cluster = _capacity is not null ? await _capacity.ReadScopeAsync(_store, cancellationToken).ConfigureAwait(false)
            : _store.GetService(typeof(IAsyncProjectionDestinationScope)) is IAsyncProjectionDestinationScope scope
            ? await scope.GetScopeAsync(cancellationToken).ConfigureAwait(false)
            : (_store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope;
        VerifyBoundary();
        return ProjectionTenantValidation.Scope(boundary, cluster);
    }
    private ProjectionTenantBoundary CaptureBoundary()
    {
        VerifyBoundary();
        return _boundary ??= ProjectionTenantValidation.Read(_store);
    }
    private void VerifyBoundary()
    {
        if (_boundary is not null && ProjectionTenantValidation.Read(_store) != _boundary)
        {
            throw new InvalidOperationException("The projection destination changed tenant boundary during the operation.");
        }
    }
    internal string? Destination
    {
        get
        {
            VerifyBoundary();
            return (_store.GetService(typeof(IProjectionDestinationIdentity)) as IProjectionDestinationIdentity)?.Identity;
        }
    }
    internal bool IsPositioned { get; private init; }

    internal ProjectionStoreOperations<TProjection> ResolveRequired(IReadOnlyList<ProjectionEvent> events, Func<Type, bool>? handles = null)
    {
        _ = PositionedProjectionWriter<TProjection>.ResolveRequired(_store, events);
        ProjectionTenantBoundary? boundary = null;
        foreach (var item in events)
        {
            if (item.GlobalPosition is not null && (handles is null || handles(item.Domain.GetType())))
            {
                boundary ??= CaptureBoundary();
                ProjectionTenantValidation.Validate(boundary, item.TenantId);
            }
        }
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

        return new ProjectionStoreOperations<TProjection>(store)
        {
            GetByIdAsync = capability.GetByIdAsync,
            UpsertAsync = static (_, _, _) => throw new InvalidOperationException("Generated projection processing requires positioned writes."),
            GetWithPositionAsync = capability.GetWithPositionAsync,
            UpsertAtPositionAsync = capability.UpsertAtPositionAsync,
            RefoldAtPositionAsync = capability.RefoldAtPositionAsync,
            UpsertUnnumberedAsync = capability.UpsertUnnumberedAsync,
            IsPositioned = true,
        }.WithCapacity(services);
    }


    internal ProjectionStoreOperations<TProjection> WithCapacity(IServiceProvider services)
    {
        if (_capacity is not null || services.GetService<ProjectionCapacity>() is not { } capacity) { return this; }
        _capacity = capacity;
        var read = _getByIdAsync;
        var write = _upsertAsync;
        var positionedRead = _getWithPositionAsync;
        var advance = _upsertAtPositionAsync;
        var refold = _refoldAtPositionAsync;
        var unnumbered = _upsertUnnumberedAsync;
        _getByIdAsync = (id, token) => RunAsync(ProjectionCapacityKind.Read, ct => new ValueTask<TProjection?>(read(id, ct)), token);
        _getWithPositionAsync = (id, token) => RunAsync(ProjectionCapacityKind.Read,
            ct => new ValueTask<(TProjection? Projection, ProjectionPosition Position)>(positionedRead(id, ct)), token);
        _upsertAtPositionAsync = (id, value, expected, next, token) => RunAsync(ProjectionCapacityKind.Write,
            ct => new ValueTask<ProjectionAdvanceResult>(advance(id, value, expected, next, ct)), token);
        _refoldAtPositionAsync = (id, value, position, token) => RunAsync(ProjectionCapacityKind.Write,
            ct => new ValueTask<ProjectionRefoldResult>(refold(id, value, position, ct)), token);
        _upsertAsync = (id, value, token) => RunAsync(ProjectionCapacityKind.Write,
            async ct => { await write(id, value, ct).ConfigureAwait(false); return true; }, token);
        _upsertUnnumberedAsync = (id, value, token) => RunAsync(ProjectionCapacityKind.Write,
            async ct => { await unnumbered(id, value, ct).ConfigureAwait(false); return true; }, token);
        return this;

        Task<TResult> RunAsync<TResult>(ProjectionCapacityKind kind, Func<CancellationToken, ValueTask<TResult>> operation, CancellationToken token) =>
            capacity.ExecuteStoreAsync(_store, kind, ct => { VerifyBoundary(); return operation(ct); }, token).AsTask();
    }

    private static Task<(TProjection?, ProjectionPosition)> MissingRead(string id, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
    private static Task<ProjectionAdvanceResult> MissingAdvance(string id, TProjection state, long? expected, long next, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
    private static Task<ProjectionRefoldResult> MissingRefold(string id, TProjection state, long position, CancellationToken token) => throw new InvalidOperationException("No positioned store.");
}
