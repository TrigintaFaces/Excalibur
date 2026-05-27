// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// ISP sub-interface for projection stores that support optimized existence checks.
/// </summary>
/// <remarks>
/// <para>
/// Providers implement this interface to offer a faster existence check than the
/// default <see cref="IProjectionStore{TProjection}.GetByIdAsync"/> fallback.
/// For example, SQL Server providers can use <c>SELECT TOP 1 1 WHERE Id = @id</c>
/// without deserializing the full projection state.
/// </para>
/// <para>
/// Consumers use the extension method <see cref="ProjectionStoreExtensions.ExistsAsync{TProjection}"/>
/// which automatically detects and uses this interface when available:
/// <code>
/// bool exists = await store.ExistsAsync("order-123", ct);
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface IExistsProjectionStore<TProjection> : IProjectionStore<TProjection>
    where TProjection : class
{
    /// <summary>
    /// Checks whether a projection with the specified identifier exists
    /// using a provider-optimized query that avoids full deserialization.
    /// </summary>
    /// <param name="id">The projection identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if a projection with the specified <paramref name="id"/> exists;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);
}
