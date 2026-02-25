// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// In-memory implementation of <see cref="ILegalHoldStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
public sealed class InMemoryLegalHoldStore : ILegalHoldStore, ILegalHoldQueryStore
{
	private readonly ConcurrentDictionary<Guid, LegalHold> _holds = new();

	/// <summary>
	/// Gets the count of holds in the store.
	/// </summary>
	public int HoldCount => _holds.Count;

	/// <summary>
	/// Gets the count of active holds in the store.
	/// </summary>
	public int ActiveHoldCount => _holds.Values.Count(h => h.IsActive);

	/// <inheritdoc />
	public Task SaveHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);

		if (!_holds.TryAdd(hold.HoldId, hold))
		{
			throw new InvalidOperationException($"Legal hold {hold.HoldId} already exists");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<LegalHold?> GetHoldAsync(
		Guid holdId,
		CancellationToken cancellationToken)
	{
		_ = _holds.TryGetValue(holdId, out var hold);
		return Task.FromResult(hold);
	}

	/// <inheritdoc />
	public Task<bool> UpdateHoldAsync(
		LegalHold hold,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hold);

		if (!_holds.TryGetValue(hold.HoldId, out _))
		{
			return Task.FromResult(false);
		}

		_holds[hold.HoldId] = hold;
		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> GetActiveHoldsForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectIdHash);

		var query = _holds.Values
			.Where(h => h.IsActive && h.DataSubjectIdHash == dataSubjectIdHash);

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => h.TenantId == tenantId || h.TenantId is null);
		}

		// Exclude expired holds
		var now = DateTimeOffset.UtcNow;
		query = query.Where(h => !h.ExpiresAt.HasValue || h.ExpiresAt.Value > now);

		return Task.FromResult<IReadOnlyList<LegalHold>>(query.ToList());
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> GetActiveHoldsForTenantAsync(
		string tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

		var now = DateTimeOffset.UtcNow;

		var holds = _holds.Values
			.Where(h => h.IsActive &&
						h.TenantId == tenantId &&
						(!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now))
			.ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var query = _holds.Values
			.Where(h => h.IsActive && (!h.ExpiresAt.HasValue || h.ExpiresAt.Value > now));

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => h.TenantId == tenantId);
		}

		var holds = query.OrderByDescending(h => h.CreatedAt).ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> ListAllHoldsAsync(
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		CancellationToken cancellationToken)
	{
		var query = _holds.Values.AsEnumerable();

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(h => h.TenantId == tenantId);
		}

		if (fromDate.HasValue)
		{
			query = query.Where(h => h.CreatedAt >= fromDate.Value);
		}

		if (toDate.HasValue)
		{
			query = query.Where(h => h.CreatedAt <= toDate.Value);
		}

		var holds = query.OrderByDescending(h => h.CreatedAt).ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(holds);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<LegalHold>> GetExpiredHoldsAsync(
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var expiredHolds = _holds.Values
			.Where(h => h.IsActive && h.ExpiresAt.HasValue && h.ExpiresAt.Value <= now)
			.ToList();

		return Task.FromResult<IReadOnlyList<LegalHold>>(expiredHolds);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(ILegalHoldQueryStore))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Clears all holds from the store.
	/// </summary>
	public void Clear()
	{
		_holds.Clear();
	}
}
