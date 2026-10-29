// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.EventSourcing;

/// <summary>A read-only handle pinned to one exact projection destination for one operation.</summary>
/// <typeparam name="TProjection">The explicitly supported read schema.</typeparam>
/// <remarks>Disposal releases this handle, never a client borrowed from the host. No method provisions storage.</remarks>
public interface IProjectionGenerationReader<TProjection> : IAsyncDisposable where TProjection : class
{
    /// <summary>Gets the storage scope this handle last resolved.</summary>
    /// <value>A credential-free storage scope, or null when no resolve has run yet.</value>
    /// <remarks>
    /// <b>Null means "not resolved yet", and nothing else.</b> It does NOT mean the scope cannot be
    /// established — that case is an exception from <see cref="GetDestinationScopeAsync"/>, because a
    /// handle that cannot name the storage it addresses cannot certify a generation, and a caller
    /// comparing a recorded scope against null cannot tell an unresolvable client from an unverified
    /// one. Providers that derive the scope from configuration may answer immediately; providers that
    /// resolve it from storage metadata answer null until the resolve has run, and MUST answer the
    /// resolved value afterwards. Answering null once the scope IS established states the opposite of
    /// the truth and is a conformance failure.
    /// </remarks>
    string? DestinationScope { get; }

    /// <summary>Resolves the actual client scope before reading; does not provision storage.</summary>
    /// <param name="cancellationToken">Cancels metadata resolution.</param>
    /// <returns>The verified scope. Never null and never blank.</returns>
    /// <exception cref="InvalidOperationException">
    /// The storage scope cannot be established, so this handle cannot verify which storage it addresses.
    /// </exception>
    ValueTask<string> GetDestinationScopeAsync(CancellationToken cancellationToken)
        => DestinationScope is { } scope && !string.IsNullOrWhiteSpace(scope)
            ? ValueTask.FromResult(scope)
            : throw new InvalidOperationException(
                "This projection generation handle cannot establish the storage scope of the client it "
                + "uses, so it cannot verify which storage it addresses. Configure the store with a "
                + "resolvable endpoint, account or data source; a generation read cannot be certified "
                + "against a client whose storage is unknown.");

	/// <summary>Reads a projection in the ambient tenant.</summary>
	/// <param name="id">Projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection, or null when absent.</returns>
	Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken);
	/// <summary>Queries the selected generation using its supported field names.</summary>
	/// <param name="filters">Provider-supported filters.</param>
	/// <param name="options">Sorting and pagination.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching projections in the ambient tenant.</returns>
	Task<IReadOnlyList<TProjection>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken);
	/// <summary>Counts matching projections in the ambient tenant.</summary>
	/// <param name="filters">Provider-supported filters.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching count.</returns>
	Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken);
}

/// <summary>Optional capability for opening exact destinations with the provider's conventional serializer.</summary>
/// <typeparam name="TProjection">The read schema.</typeparam>
/// <remarks>Resolve through the outer configured store. Decorators must preserve tenant and encryption boundaries.
/// Every opened handle must enforce exactly the originating store's <see cref="IProjectionTenantBoundary"/>
/// and reject operations if that boundary changes during its lifetime. Selecting another destination must
/// never widen a partitioned store to estate-wide access.
/// Opening constructs a handle without I/O, version qualification, or provisioning. Writers remain pinned.</remarks>
public interface IProjectionGenerationReaderFactory<TProjection> where TProjection : class
{
	/// <summary>Opens a read-only handle using the provider's conventional serializer.</summary>
	/// <param name="destinationIdentity">The exact canonical destination from a durable generation pointer.</param>
	/// <returns>A handle to that destination.</returns>
	[RequiresUnreferencedCode("The provider's conventional projection serializer can require reflection. Use the generated reader capability for trimming.")]
	[RequiresDynamicCode("The provider's conventional projection serializer can require dynamic code. Use the generated reader capability for NativeAOT.")]
	IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity);
}

/// <summary>Optional capability for exact-destination reads using generated JSON metadata without reflective fallback.</summary>
/// <typeparam name="TProjection">The read schema.</typeparam>
/// <remarks>Every opened handle must preserve the originating outer store's tenant and encryption boundaries,
/// enforce exactly its <see cref="IProjectionTenantBoundary"/>, and reject operations if that boundary changes
/// during the handle's lifetime. Selecting another destination must never widen a partitioned store to estate-wide access.</remarks>
public interface IAotProjectionGenerationReaderFactory<TProjection> where TProjection : class
{
	/// <summary>Opens a read-only handle bound to the supplied generated serializer.</summary>
	/// <param name="destinationIdentity">The exact canonical destination from a durable generation pointer.</param>
	/// <param name="typeInfo">Generated metadata for a schema compatible with this destination and its query fields.</param>
	/// <returns>A handle to that destination.</returns>
	IProjectionGenerationReader<TProjection> OpenReader(string destinationIdentity, JsonTypeInfo<TProjection> typeInfo);
}
