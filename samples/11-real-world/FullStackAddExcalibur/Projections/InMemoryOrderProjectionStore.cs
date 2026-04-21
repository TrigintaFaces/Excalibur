// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace FullStackAddExcalibur.Projections;

/// <summary>
/// Demo in-memory projection store for the full-stack sample.
/// </summary>
/// <remarks>
/// <para>
/// Keeps the sample runnable without an ElasticSearch cluster while still
/// demonstrating the canonical write-side -> event -> projection flow.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/> so concurrent
/// projection updates from multiple event handlers work correctly.
/// </para>
/// </remarks>
public sealed class InMemoryOrderProjectionStore : IOrderProjectionStore
{
	private readonly ConcurrentDictionary<Guid, OrderReadModel> _projections = new();

	/// <inheritdoc />
	public ValueTask<OrderReadModel?> GetAsync(Guid orderId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(_projections.TryGetValue(orderId, out var model) ? model : null);
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<OrderReadModel>> ListAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<OrderReadModel> snapshot = [.. _projections.Values];
		return ValueTask.FromResult(snapshot);
	}

	/// <inheritdoc />
	public ValueTask UpsertAsync(OrderReadModel model, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(model);
		cancellationToken.ThrowIfCancellationRequested();
		_projections[model.OrderId] = model;
		return ValueTask.CompletedTask;
	}
}
