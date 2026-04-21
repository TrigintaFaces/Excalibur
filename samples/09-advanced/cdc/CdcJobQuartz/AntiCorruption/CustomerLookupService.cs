// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace CdcJobQuartz.AntiCorruption;

/// <summary>
/// Service for looking up customer IDs by external legacy IDs.
/// </summary>
public interface ICustomerLookupService
{
	/// <summary>
	/// Registers a mapping between external customer ID and customer ID.
	/// </summary>
	void RegisterMapping(string externalCustomerId, Guid customerId);

	/// <summary>
	/// Gets the customer ID for an external customer ID.
	/// </summary>
	Guid? GetCustomerId(string externalCustomerId);
}

/// <summary>
/// In-memory implementation of <see cref="ICustomerLookupService"/>.
/// In production, this would be backed by <see cref="Excalibur.Data.IdentityMap.IIdentityMapStore"/>.
/// </summary>
public sealed class InMemoryCustomerLookupService : ICustomerLookupService
{
	private readonly Dictionary<string, Guid> _externalToInternal = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc/>
	public void RegisterMapping(string externalCustomerId, Guid customerId)
	{
		_externalToInternal[externalCustomerId] = customerId;
	}

	/// <inheritdoc/>
	public Guid? GetCustomerId(string externalCustomerId)
	{
		return _externalToInternal.TryGetValue(externalCustomerId, out var id) ? id : null;
	}
}
