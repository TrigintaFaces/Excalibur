// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace FullStackAddExcalibur.Projections;

/// <summary>
/// Read-side projection store for <see cref="OrderReadModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// This sample uses an in-memory implementation so the operational flow can be
/// exercised end-to-end without provisioning ElasticSearch. A production deployment
/// would swap this for an ElasticSearch-backed implementation using the
/// <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/> already wired
/// in <c>Program.cs</c>, or for any other read-model store.
/// </para>
/// <para>
/// The projection handlers in <see cref="OrderProjectionHandlers"/> update this
/// store in response to domain events, independent of the write-side event store.
/// </para>
/// </remarks>
public interface IOrderProjectionStore
{
	/// <summary>
	/// Gets the projected read model for the specified order.
	/// </summary>
	/// <param name="orderId">The aggregate identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The read model if a projection exists; otherwise <see langword="null"/>.</returns>
	ValueTask<OrderReadModel?> GetAsync(Guid orderId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets all projected orders. Intended for sample browsing; a real store would support paging.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A snapshot of all known projections.</returns>
	ValueTask<IReadOnlyList<OrderReadModel>> ListAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates the projection for the specified order.
	/// </summary>
	/// <param name="model">The read model to upsert.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	ValueTask UpsertAsync(OrderReadModel model, CancellationToken cancellationToken);
}
