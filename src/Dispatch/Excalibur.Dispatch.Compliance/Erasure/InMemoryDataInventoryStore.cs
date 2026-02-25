// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// In-memory implementation of <see cref="IDataInventoryStore"/> for development and testing.
/// </summary>
/// <remarks>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </remarks>
public sealed class InMemoryDataInventoryStore : IDataInventoryStore, IDataInventoryQueryStore
{
	private readonly ConcurrentDictionary<string, DataLocationRegistration> _registrations = new();
	private readonly ConcurrentDictionary<string, List<DataLocation>> _discoveredLocations = new();
#if NET9_0_OR_GREATER

	private readonly Lock _locationsLock = new();

#else

	private readonly object _locationsLock = new();

#endif

	/// <inheritdoc />
	public Task SaveRegistrationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);

		var key = GetRegistrationKey(registration.TableName, registration.FieldName);
		_registrations[key] = registration;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> RemoveRegistrationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken)
	{
		var key = GetRegistrationKey(tableName, fieldName);
		return Task.FromResult(_registrations.TryRemove(key, out _));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<DataLocationRegistration>>(
			_registrations.Values.ToList());
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataLocationRegistration>> FindRegistrationsForDataSubjectAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var query = _registrations.Values
			.Where(r => r.IdType == idType);

		if (!string.IsNullOrEmpty(tenantId))
		{
			query = query.Where(r =>
				string.IsNullOrEmpty(r.TenantIdColumn) ||
				r.TenantIdColumn == tenantId);
		}

		return Task.FromResult<IReadOnlyList<DataLocationRegistration>>(query.ToList());
	}

	/// <inheritdoc />
	public Task RecordDiscoveredLocationAsync(
		DataLocation location,
		string dataSubjectId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(location);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

		lock (_locationsLock)
		{
			if (!_discoveredLocations.TryGetValue(dataSubjectId, out var locations))
			{
				locations = [];
				_discoveredLocations[dataSubjectId] = locations;
			}

			// Check if location already exists
			var existing = locations.FirstOrDefault(l =>
				l.TableName == location.TableName &&
				l.FieldName == location.FieldName &&
				l.RecordId == location.RecordId);

			if (existing is null)
			{
				locations.Add(location);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataLocation>> GetDiscoveredLocationsAsync(
		string dataSubjectId,
		CancellationToken cancellationToken)
	{
		if (_discoveredLocations.TryGetValue(dataSubjectId, out var locations))
		{
			return Task.FromResult<IReadOnlyList<DataLocation>>(locations.ToList());
		}

		return Task.FromResult<IReadOnlyList<DataLocation>>([]);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		// Build data map from registrations
		var entries = _registrations.Values
			.GroupBy(r => new { r.TableName, r.FieldName, r.DataCategory })
			.Select(g => new DataMapEntry
			{
				TableName = g.Key.TableName,
				FieldName = g.Key.FieldName,
				DataCategory = g.Key.DataCategory,
				IsAutoDiscovered = false,
				RecordCount = CountRecordsForLocation(g.Key.TableName, g.Key.FieldName),
				Description = g.First().Description
			})
			.ToList();

		// Add discovered locations not in registrations
		foreach (var kvp in _discoveredLocations)
		{
			foreach (var location in kvp.Value)
			{
				if (!entries.Any(e => e.TableName == location.TableName && e.FieldName == location.FieldName))
				{
					entries.Add(new DataMapEntry
					{
						TableName = location.TableName,
						FieldName = location.FieldName,
						DataCategory = location.DataCategory,
						IsAutoDiscovered = location.IsAutoDiscovered,
						RecordCount = 1
					});
				}
			}
		}

		return Task.FromResult<IReadOnlyList<DataMapEntry>>(entries);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IDataInventoryQueryStore))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Gets the count of registrations in the store.
	/// </summary>
	public int RegistrationCount => _registrations.Count;

	/// <summary>
	/// Gets the count of data subjects with discovered locations.
	/// </summary>
	public int DataSubjectCount => _discoveredLocations.Count;

	/// <summary>
	/// Clears all data from the store.
	/// </summary>
	public void Clear()
	{
		_registrations.Clear();
		_discoveredLocations.Clear();
	}

	private static string GetRegistrationKey(string tableName, string fieldName) =>
		$"{tableName}:{fieldName}".ToUpperInvariant();

	private long CountRecordsForLocation(string tableName, string fieldName)
	{
		var count = 0L;
		foreach (var locations in _discoveredLocations.Values)
		{
			count += locations.Count(l => l.TableName == tableName && l.FieldName == fieldName);
		}
		return count;
	}
}
