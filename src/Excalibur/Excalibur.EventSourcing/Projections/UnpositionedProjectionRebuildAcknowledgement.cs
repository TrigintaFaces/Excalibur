// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Records that this host has accepted rebuilding a projection whose store records no positions.
/// </summary>
/// <remarks>
/// Carries no state and is never resolved for behavior; its presence in the container is the whole
/// signal.
/// </remarks>
internal sealed class UnpositionedProjectionRebuildAcknowledgement;

/// <summary>
/// Opts a host into rebuilding projections on a store that records no positions.
/// </summary>
public static class UnpositionedProjectionRebuildServiceCollectionExtensions
{
	/// <summary>
	/// Accepts that a rebuild on a projection store without
	/// <c>IPositionedProjectionStore&lt;TProjection&gt;</c> cannot detect a concurrent writer, and that
	/// every other writer of those rows has been stopped for the duration of the rebuild.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>What goes wrong without stopping the other writers.</b> A rebuild replays the stream to some
	/// position and writes the state it folded. On a store that records positions that write is
	/// conditional, so a writer that moved the row in the meantime is detected and the losing write is
	/// refused visibly. Without positions there is no coordinate to be conditional on, so the write is a
	/// blind overwrite:
	/// </para>
	/// <code>
	/// rebuild  replays to P, folds {count: 100}
	/// host     applies the event at P+1, commits  {count: 101}
	/// rebuild  writes {count: 100}        &lt;- nothing refuses it
	/// </code>
	/// <para>
	/// The event at <c>P+1</c> is committed and absent from the read model permanently, and BOTH
	/// operations report success. Nothing downstream ever learns.
	/// </para>
	/// <para>
	/// <b>The alternative, and it is the right answer in production:</b> use a projection store that
	/// implements <c>IPositionedProjectionStore&lt;TProjection&gt;</c>. Every persistent store this
	/// framework ships does — SQL Server, PostgreSQL, Elasticsearch, OpenSearch, MongoDB, Cosmos DB,
	/// DynamoDB and Firestore. This method exists for the cases where no coordinate is available and the
	/// host can guarantee exclusivity by other means: a consumer's own simple store, or a test.
	/// </para>
	/// <para>
	/// <b>This acknowledges exclusivity, not safety.</b> It does not make the rebuild safe; it records
	/// that you have made it safe by stopping the projection host and any other writer of those rows
	/// first.
	/// </para>
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection, for chaining.</returns>
	public static IServiceCollection AllowUnpositionedProjectionRebuild(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<UnpositionedProjectionRebuildAcknowledgement>();

		return services;
	}
}
