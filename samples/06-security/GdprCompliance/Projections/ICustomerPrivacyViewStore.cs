// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace GdprCompliance.Projections;

/// <summary>
/// Read-side store for <see cref="CustomerPrivacyView"/>.
/// </summary>
public interface ICustomerPrivacyViewStore
{
	/// <summary>Gets the privacy view for the specified customer.</summary>
	ValueTask<CustomerPrivacyView?> GetAsync(Guid customerId, CancellationToken cancellationToken);

	/// <summary>Creates or updates the privacy view for the specified customer.</summary>
	ValueTask UpsertAsync(CustomerPrivacyView view, CancellationToken cancellationToken);
}

/// <summary>
/// In-memory privacy view store for the sample. Thread-safe.
/// </summary>
public sealed class InMemoryCustomerPrivacyViewStore : ICustomerPrivacyViewStore
{
	private readonly ConcurrentDictionary<Guid, CustomerPrivacyView> _views = new();

	/// <inheritdoc />
	public ValueTask<CustomerPrivacyView?> GetAsync(Guid customerId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(_views.TryGetValue(customerId, out var view) ? view : null);
	}

	/// <inheritdoc />
	public ValueTask UpsertAsync(CustomerPrivacyView view, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(view);
		cancellationToken.ThrowIfCancellationRequested();
		_views[view.CustomerId] = view;
		return ValueTask.CompletedTask;
	}
}
