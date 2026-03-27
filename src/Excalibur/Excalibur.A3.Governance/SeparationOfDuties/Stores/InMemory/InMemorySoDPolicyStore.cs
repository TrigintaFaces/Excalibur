// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.A3.Governance.SeparationOfDuties;

namespace Excalibur.A3.Governance.Stores.InMemory;

/// <summary>
/// In-memory implementation of <see cref="ISoDPolicyStore"/> backed by
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intended for development, testing, and standalone scenarios where no persistent store
/// is configured. Registered as a singleton fallback via <c>TryAddSingleton</c> in
/// <c>AddSeparationOfDuties()</c>.
/// </para>
/// </remarks>
internal sealed class InMemorySoDPolicyStore : ISoDPolicyStore
{
	private readonly ConcurrentDictionary<string, SoDPolicy> _policies =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public Task<SoDPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken)
	{
		_policies.TryGetValue(policyId, out var policy);
		return Task.FromResult(policy);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SoDPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<SoDPolicy>>(_policies.Values.ToList());
	}

	/// <inheritdoc />
	public Task SavePolicyAsync(SoDPolicy policy, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);
		_policies[policy.PolicyId] = policy;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> DeletePolicyAsync(string policyId, CancellationToken cancellationToken)
	{
		return Task.FromResult(_policies.TryRemove(policyId, out _));
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}
}
