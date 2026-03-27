// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Governance.Provisioning;

namespace Excalibur.A3.Governance.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IProvisioningStore"/> for development and testing.
/// </summary>
internal sealed class InMemoryProvisioningStore : IProvisioningStore
{
	private readonly ConcurrentDictionary<string, ProvisioningRequestSummary> _requests = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<ProvisioningRequestSummary?> GetRequestAsync(
		string requestId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(requestId);
		_requests.TryGetValue(requestId, out var request);
		return Task.FromResult(request);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ProvisioningRequestSummary>> GetRequestsByStatusAsync(
		ProvisioningRequestStatus? status,
		CancellationToken cancellationToken)
	{
		IReadOnlyList<ProvisioningRequestSummary> result = status.HasValue
			? _requests.Values.Where(r => r.Status == status.Value).ToList()
			: _requests.Values.ToList();
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task SaveRequestAsync(
		ProvisioningRequestSummary request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		_requests[request.RequestId] = request;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> DeleteRequestAsync(
		string requestId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(requestId);
		return Task.FromResult(_requests.TryRemove(requestId, out _));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}
}
