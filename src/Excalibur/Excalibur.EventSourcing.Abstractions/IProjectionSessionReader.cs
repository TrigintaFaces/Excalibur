// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing;

/// <summary>
/// Reads a projection, optionally waiting until it has caught up to a write the caller made.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the read half of session consistency.</b> A save returns a <see cref="SaveResult"/>
/// carrying a session token; passing that token here makes the read observe the write that produced it,
/// or say plainly that it could not confirm so within the configured bound.
/// </para>
/// <para>
/// <b>The WRITE does not wait; the READ does.</b> Making the save wait for a projection would put that
/// latency on every save, including the ones nobody reads back. Here only the caller who needs the
/// guarantee pays for it. This is the shape Azure Cosmos DB uses for session consistency, and the
/// reason is the same.
/// </para>
/// <para>
/// <b>A separate interface rather than a member on <see cref="IProjectionStore{TProjection}"/>.</b> That
/// interface already carries five methods, the ceiling this codebase holds interfaces to, and a sixth
/// would also oblige every store provider to implement host-progress logic that has nothing to do with
/// storing a projection. This way no provider changes at all.
/// </para>
/// <para>
/// <b>The read and the wait are one call on purpose.</b> Exposing a bare "wait" primitive would leave
/// the caller free to read first and wait afterwards, which looks correct and is not.
/// </para>
/// </remarks>
public interface IProjectionSessionReader
{
	/// <summary>
	/// Reads a projection, first waiting — up to the configured bound — until it has applied the write
	/// the supplied session describes.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to read.</typeparam>
	/// <param name="projectionId">The projection identifier.</param>
	/// <param name="session">
	/// The result of the save this read must observe. A session whose token is <see langword="null"/>
	/// requests no consistency and is read immediately.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection, with what can be said about how current it was.</returns>
	[RequiresUnreferencedCode("Projection stores serialize the projection type reflectively.")]
	[RequiresDynamicCode("Projection stores serialize the projection type reflectively.")]
	ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(
		string projectionId,
		SaveResult session,
		CancellationToken cancellationToken)
		where TProjection : class;

    /// <summary>Reads a projection through generated-metadata persistence after waiting for the session token.</summary>
    /// <typeparam name="TProjection">Projection state type.</typeparam>
    /// <param name="projectionId">Projection identifier.</param>
    /// <param name="session">The write to observe.</param>
    /// <param name="typeInfo">The same generated metadata instance used by the store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projection and its freshness.</returns>
    /// <remarks>The default implementation refuses this capability. It never calls the reflective overload.</remarks>
    ValueTask<ProjectionRead<TProjection>> ReadAsync<TProjection>(string projectionId, SaveResult session,
        JsonTypeInfo<TProjection> typeInfo, CancellationToken cancellationToken) where TProjection : class
        => throw new NotSupportedException("This session reader does not support generated-metadata reads.");
}
