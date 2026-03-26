// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Builds projections on-demand by replaying events without persisting the result.
/// Equivalent to Marten's "Live" projection mode.
/// </summary>
/// <remarks>
/// <para>
/// Ephemeral projections replay all events for a given aggregate and return a fully-hydrated
/// projection instance. The result is NOT stored in any projection store.
/// </para>
/// <para>
/// Ephemeral projections use the same <c>When&lt;T&gt;</c> handlers registered via
/// <c>IProjectionBuilder&lt;T&gt;</c>, ensuring consistency with inline and async modes.
/// </para>
/// <para>
/// Optional caching via <c>IDistributedCache</c> is supported when the projection
/// is configured with <c>.WithCacheTtl()</c>.
/// </para>
/// </remarks>
public interface IEphemeralProjectionEngine
{
	/// <summary>
	/// Builds a projection by replaying all events for the specified aggregate.
	/// </summary>
	/// <typeparam name="TProjection">The projection state type.</typeparam>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A fully-hydrated projection built from the aggregate's event stream.</returns>
	Task<TProjection> BuildAsync<TProjection>(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		where TProjection : class, new();
}
