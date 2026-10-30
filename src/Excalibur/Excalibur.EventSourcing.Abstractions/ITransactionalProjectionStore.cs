// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing;

/// <summary>Optional persistence of a complete aggregate-local model in the event transaction.</summary>
/// <typeparam name="TProjection">The model type whose serializer produced the JSON.</typeparam>
/// <remarks>
/// Resolve through the projection store's capability surface. Implementations must reject mismatched
/// destinations and preserve tenant and retirement boundaries. This operation advances no global cursor.
/// </remarks>
public interface ITransactionalProjectionStore<TProjection> where TProjection : class
{
    /// <summary>Serializes a model with the destination's configured wire format.</summary>
    /// <param name="projection">Model to persist.</param>
    /// <returns>Serialized model.</returns>
    [RequiresUnreferencedCode("Projection serialization may use reflection. Use generated metadata instead.")]
    [RequiresDynamicCode("Projection serialization may require dynamic code. Use generated metadata instead.")]
    string Serialize(TProjection projection);

    /// <summary>Serializes a model with generated metadata and the destination's configured wire format.</summary>
    /// <param name="projection">Model to persist.</param>
    /// <param name="typeInfo">Generated type metadata.</param>
    /// <returns>Serialized model.</returns>
    string Serialize(TProjection projection, JsonTypeInfo<TProjection> typeInfo);

    /// <summary>Writes serialized state on the supplied transaction without committing it.</summary>
    /// <param name="transaction">Owning event transaction and source boundary.</param>
    /// <param name="aggregateId">Projection key, equal to the aggregate identifier.</param>
    /// <param name="aggregateType">The one aggregate type this destination belongs to.</param>
    /// <param name="json">Complete model serialized with registered projection metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completed when the enlisted write finishes.</returns>
    ValueTask WriteAsync(ProjectionTransaction transaction, string aggregateId, string aggregateType,
        string json, CancellationToken cancellationToken);
}
