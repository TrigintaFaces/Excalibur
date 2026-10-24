// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing;

/// <summary>Provides generated-metadata projection operations without reflection fallback.</summary>
/// <typeparam name="TProjection">The projection state.</typeparam>
/// <remarks>
/// Resolve this capability through the outer <see cref="IProjectionStore{TProjection}"/>.
/// Every operation must preserve that store's tenant, encryption and position guarantees.
/// Decorators must mediate these operations or decline the capability; forwarding an inner
/// store is not sufficient. The positioned operations have exactly the concurrency and refusal
/// semantics of <see cref="IPositionedProjectionStore{TProjection}"/>. The outer store must also expose that positioned capability for shared host startup validation.
/// </remarks>
public interface IAotProjectionStore<TProjection> where TProjection : class
{
    /// <summary>Gets the registration metadata identifying the generated context. Operations must use that context with the actual source serializer settings to preserve the stored format.</summary>
    JsonTypeInfo<TProjection> TypeInfo { get; }

    /// <summary>Reads the state, confined to the store's ambient tenant.</summary>
    /// <param name="id">Projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state, or null when absent.</returns>
    Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Reads the state and position atomically.</summary>
    /// <param name="id">Projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state and its position from the same observation.</returns>
    Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id, CancellationToken cancellationToken);

    /// <summary>Conditionally advances from the expected position; never replaces a refused write.</summary>
    /// <param name="id">Projection identifier.</param>
    /// <param name="projection">Folded state.</param>
    /// <param name="expectedPosition">Position read before folding, or null for creation.</param>
    /// <param name="newPosition">Position covered by the fold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authoritative conditional-write outcome.</returns>
    Task<ProjectionAdvanceResult> UpsertAtPositionAsync(string id, TProjection projection, long? expectedPosition, long newPosition, CancellationToken cancellationToken);

    /// <summary>Conditionally replaces a complete fold while preserving its position.</summary>
    /// <param name="id">Projection identifier.</param>
    /// <param name="projection">Replayed state.</param>
    /// <param name="atPosition">Position read before replay.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authoritative refold outcome.</returns>
    Task<ProjectionRefoldResult> RefoldAtPositionAsync(string id, TProjection projection, long atPosition, CancellationToken cancellationToken);

    /// <summary>Stores a complete fold whose coordinate is unknown, explicitly marking it unnumbered.</summary>
    /// <param name="id">Projection identifier.</param>
    /// <param name="projection">Complete folded state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the write.</returns>
    Task UpsertUnnumberedAsync(string id, TProjection projection, CancellationToken cancellationToken);
}


