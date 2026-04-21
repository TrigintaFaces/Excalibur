// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace GdprCompliance.Domain;

/// <summary>
/// Minimal customer repository contract for the GDPR sample.
/// </summary>
public interface ICustomerRepository
{
	/// <summary>Finds a customer by id.</summary>
	Task<Customer?> FindAsync(Guid id);

	/// <summary>Inserts or updates a customer.</summary>
	Task SaveAsync(Customer customer);

	/// <summary>Tombstones a customer (keep id, drop all PII).</summary>
	Task TombstoneAsync(Guid id);
}

/// <summary>
/// In-memory implementation for the demo.
/// </summary>
public sealed class InMemoryCustomerRepository : ICustomerRepository
{
	private readonly ConcurrentDictionary<Guid, Customer> _store = new();

	/// <inheritdoc />
	public Task<Customer?> FindAsync(Guid id)
	{
		_ = _store.TryGetValue(id, out var customer);
		return Task.FromResult(customer);
	}

	/// <inheritdoc />
	public Task SaveAsync(Customer customer)
	{
		ArgumentNullException.ThrowIfNull(customer);
		_store[customer.Id] = customer;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task TombstoneAsync(Guid id)
	{
		// Replace with a marker customer that keeps only the id and a sentinel value.
		_store[id] = new Customer
		{
			Id = id,
			FullName = "<erased>",
			Email = "<erased>",
			PhoneNumber = null,
			NationalIdNumber = null,
			RegisteredAt = default
		};
		return Task.CompletedTask;
	}
}
