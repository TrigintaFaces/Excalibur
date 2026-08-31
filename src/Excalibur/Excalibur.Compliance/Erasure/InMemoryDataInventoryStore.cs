// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// In-memory implementation of <see cref="IDataInventoryStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// This implementation stores all data in memory and is NOT suitable for production use.
/// Data is lost when the application restarts.
/// </para>
/// <para>
/// Every read and write is confined to the caller's tenant, matching the durable stores. An in-memory
/// store that scoped differently from the store it stands in for would let a consumer validate against
/// one isolation semantic and deploy onto another.
/// </para>
/// </remarks>
internal sealed class InMemoryDataInventoryStore : IDataInventoryStore, IDataInventoryQueryStore
{
	private readonly ConcurrentDictionary<string, TenantScopedRegistration> _registrations = new();
	private readonly ConcurrentDictionary<string, List<DataLocation>> _discoveredLocations = new();
	private readonly Lock _locationsLock = new();
	private readonly ITenantContext _tenantContext;

	// Deployment MODE, read from TenantContextOptions.RequireTenant (set by AddMultiTenancy()). This is NOT
	// "is an ITenantContext present": the framework always registers a single-tenant default, so presence
	// would report every deployment as multi-tenant.
	private readonly bool _requireTenant;
	/// <summary>
	/// Gets the keyed tenant partition this store reads and writes under, resolved in one place so every
	/// statement it builds binds the same term.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Deployment mode decides the shape, and it is read from <see cref="TenantContextOptions.RequireTenant"/>
	/// -- the flag the multi-tenancy composition sets -- never inferred from whether an
	/// <see cref="ITenantContext"/> happens to be registered. The framework always registers a single-tenant
	/// default context, so presence would make every deployment look multi-tenant; worse, it made the stored
	/// term depend on whether some UNRELATED feature had registered a context, so two hosts with identical
	/// inventory configuration filed rows under different tenant identifiers.
	/// </para>
	/// <para>
	/// A single-tenant deployment binds the reserved untenanted partition -- a concrete term, never an absent
	/// one, and the same term this table's column defaults to. A multi-tenant deployment binds the resolved
	/// ambient tenant and fails closed when none is established.
	/// </para>
	/// </remarks>
	private KeyedTenantPartition CurrentTenantPartition =>
		_requireTenant ? KeyedTenantPartition.FromContext(_tenantContext) : KeyedTenantPartition.Untenanted;


	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryDataInventoryStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: the store resolves its partition from here in multi-tenant
	/// mode, and a single-tenant host receives the framework default context, so there is no state in
	/// which the partition is undecided.
	/// </param>
	/// <param name="tenantContextOptions">
	/// The tenant-context options. Its <see cref="TenantContextOptions.RequireTenant"/> (set by
	/// <c>AddMultiTenancy()</c>) selects the deployment mode. Required, and required for the reason the
	/// mode must not be inferred: an omitted binding would be indistinguishable from a deliberate
	/// declaration of single-tenancy, and the two get different data.
	/// </param>
	public InMemoryDataInventoryStore(
		ITenantContext tenantContext,
		IOptions<TenantContextOptions> tenantContextOptions)
	{
		ArgumentNullException.ThrowIfNull(tenantContext);
		ArgumentNullException.ThrowIfNull(tenantContextOptions);

		_tenantContext = tenantContext;
		_requireTenant = tenantContextOptions.Value.RequireTenant;
	}

	/// <summary>
	/// Resolves the tenant term every read and write of this store is confined to.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Resolved from ambient context per call rather than fixed at construction: the store is a singleton
	/// and a construction-time capture would bind every caller to whichever tenant happened to be current
	/// when the container built it.
	/// </para>
	/// <para>
	/// This is the tenant VALUE. It is unrelated to <c>TenantIdColumn</c>, which is the NAME of a column in
	/// the consumer's own table — the two were previously conflated here, and that conflation is why a
	/// caller supplying a tenant received every tenant's registrations: the supplied value was compared
	/// against a column name, so the filter matched only when a caller happened to store their tenant id
	/// in the field reserved for the column's name, and returned everything otherwise.
	/// </para>
	/// </remarks>
	private string CurrentTenantTerm =>
		CurrentTenantPartition.TenantId;

	/// <inheritdoc />
	public Task SaveRegistrationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);

		// The tenant term is part of the KEY, not merely a stored field: without it two tenants registering
		// the same table and field are one entry, and the second save silently overwrites the first.
		var tenantTerm = CurrentTenantTerm;
		var key = GetRegistrationKey(tenantTerm, registration.TableName, registration.FieldName);
		_registrations[key] = new TenantScopedRegistration(tenantTerm, registration);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<bool> RemoveRegistrationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken)
	{
		// Scoped for the same reason the durable stores scope their DELETE: without the tenant term,
		// deregistering a field removes every tenant's registration for it. A registration is how the
		// erasure path knows a field holds personal data, so destroying another tenant's row silently
		// drops that field from their erasure coverage and their next erasure reports success without
		// ever visiting it.
		var key = GetRegistrationKey(CurrentTenantTerm, tableName, fieldName);
		return Task.FromResult(_registrations.TryRemove(key, out _));
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataLocationRegistration>> GetAllRegistrationsAsync(
		CancellationToken cancellationToken)
	{
		// "GetAll" means all of the CALLER'S — never all of everyone's. Unscoped, this returned the whole
		// estate's compliance inventory from a method whose name invites exactly that call.
		return Task.FromResult<IReadOnlyList<DataLocationRegistration>>(
			ScopedRegistrations().ToList());
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataLocationRegistration>> FindRegistrationsForDataSubjectAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		// The tenantId PARAMETER is deliberately not consulted, matching the durable stores: scope comes
		// from ambient context so a caller cannot widen it by passing someone else's term, and it applies
		// unconditionally so omitting the argument cannot silently disable isolation.
		var query = ScopedRegistrations().Where(r => r.IdType == idType);

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

		var subjectKey = GetSubjectKey(CurrentTenantTerm, dataSubjectId);

		lock (_locationsLock)
		{
			if (!_discoveredLocations.TryGetValue(subjectKey, out var locations))
			{
				locations = [];
				_discoveredLocations[subjectKey] = locations;
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
		// Scoped by the same composite key the write uses: a data-subject identifier is not globally
		// unique across tenants, so an unscoped lookup hands one tenant another's discovered PII locations
		// whenever the two share a subject id.
		if (_discoveredLocations.TryGetValue(GetSubjectKey(CurrentTenantTerm, dataSubjectId), out var locations))
		{
			lock (_locationsLock)
			{
				return Task.FromResult<IReadOnlyList<DataLocation>>(locations.ToList());
			}
		}

		return Task.FromResult<IReadOnlyList<DataLocation>>([]);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<DataMapEntry>> GetDataMapEntriesAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		// Scope comes from ambient context; the tenantId parameter is deliberately not consulted, matching
		// the durable stores. A data map is an estate-wide description of where personal data lives, so an
		// unscoped one is the single most disclosive read this store offers.
		var tenantTerm = CurrentTenantTerm;

		// Build data map from the caller's registrations
		var entries = ScopedRegistrations(tenantTerm)
			.GroupBy(r => new { r.TableName, r.FieldName, r.DataCategory })
			.Select(g => new DataMapEntry
			{
				TableName = g.Key.TableName,
				FieldName = g.Key.FieldName,
				DataCategory = g.Key.DataCategory,
				IsAutoDiscovered = false,
				RecordCount = CountRecordsForLocation(tenantTerm, g.Key.TableName, g.Key.FieldName),
				Description = g.First().Description
			})
			.ToList();

		// Add the caller's discovered locations not in their registrations
		foreach (var locations in ScopedLocations(tenantTerm))
		{
			foreach (var location in locations)
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
	/// Gets the count of registrations held across every tenant.
	/// </summary>
	/// <remarks>
	/// Estate-wide by design: this is a test and diagnostic affordance reporting the store's total size,
	/// not a tenant-scoped read. It returns a count, never another tenant's data.
	/// </remarks>
	public int RegistrationCount => _registrations.Count;

	/// <summary>
	/// Gets the count of tenant-and-data-subject pairs with discovered locations.
	/// </summary>
	/// <remarks>
	/// Estate-wide by design, as with <see cref="RegistrationCount"/>. Because discovered locations are
	/// keyed per tenant, one data subject known to two tenants counts twice.
	/// </remarks>
	public int DataSubjectCount => _discoveredLocations.Count;

	/// <summary>
	/// Clears all data from the store, for every tenant.
	/// </summary>
	public void Clear()
	{
		_registrations.Clear();
		_discoveredLocations.Clear();
	}

	/// <summary>
	/// Builds the registration key. The tenant term leads and keeps its case: the durable stores store it
	/// under a binary collation, so upper-casing it here would merge tenants this store must keep apart.
	/// Table and field remain case-insensitive, preserving the existing behaviour for those two.
	/// </summary>
	private static string GetRegistrationKey(string tenantTerm, string tableName, string fieldName) =>
		$"{tenantTerm}\n{$"{tableName}:{fieldName}".ToUpperInvariant()}";

	/// <summary>
	/// Builds the discovered-location key. Same construction, and for the same reason: a data-subject
	/// identifier is unique within a tenant, not across the estate.
	/// </summary>
	private static string GetSubjectKey(string tenantTerm, string dataSubjectId) =>
		$"{tenantTerm}\n{dataSubjectId}";

	/// <summary>
	/// Decides whether a row written under <paramref name="rowTenantTerm"/> is visible to a caller scoped
	/// to <paramref name="callerTenantTerm"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A caller sees their own rows AND untenanted rows. The second half is not a convenience — these
	/// registrations are the sweep list the erasure path walks, so a tenanted scope that cannot see an
	/// untenanted registration erases less than it was asked to and reports success anyway. Under-covering
	/// an erasure is a worse failure than the over-exposure this scoping exists to prevent, because the
	/// exposure is visible to whoever receives the extra rows and the missed erasure is visible to nobody.
	/// </para>
	/// <para>
	/// Visibility is deliberately asymmetric with WRITES: the untenanted partition remains distinct for
	/// save, remove, and keying, so a tenanted write can never overwrite an untenanted row or vice versa.
	/// Widening reads does not widen writes.
	/// </para>
	/// <para>
	/// Scope: REGISTRATIONS ONLY. A registration is schema metadata and names no person, so an untenanted
	/// one discloses nothing. Discovered locations carry a record and key identifying one subject's row
	/// and are matched on the tenant term exactly — do not reuse this predicate for them, or for any other
	/// subject-linked record such as an erasure request or a legal hold.
	/// </para>
	/// </remarks>
	private static bool IsVisibleTo(string rowTenantTerm, string callerTenantTerm) =>
		string.Equals(rowTenantTerm, callerTenantTerm, StringComparison.Ordinal)
		|| string.Equals(rowTenantTerm, TenantScope.UntenantedSentinel, StringComparison.Ordinal);

	private IEnumerable<DataLocationRegistration> ScopedRegistrations() => ScopedRegistrations(CurrentTenantTerm);

	private IEnumerable<DataLocationRegistration> ScopedRegistrations(string tenantTerm) =>
		_registrations.Values
			.Where(r => IsVisibleTo(r.TenantId, tenantTerm))
			.Select(r => r.Registration);

	/// <summary>
	/// Selects the discovered locations visible to <paramref name="tenantTerm"/>. Strict equality — the
	/// untenanted widening that <see cref="IsVisibleTo"/> applies to registrations MUST NOT be applied
	/// here.
	/// </summary>
	/// <remarks>
	/// The difference is the data, not the storage. A registration is schema metadata — "personal data of
	/// this category lives in this column" — and describes a shape every tenant of the deployment shares,
	/// so showing it estate-wide discloses nobody. A discovered location carries <c>RecordId</c> and
	/// <c>KeyId</c>: it points at one identified person's row. Widening that is a disclosure of subject
	/// data, which is the leak this whole bead exists to close.
	/// </remarks>
	private IEnumerable<List<DataLocation>> ScopedLocations(string tenantTerm)
	{
		var ownPrefix = $"{tenantTerm}\n";

		return _discoveredLocations
			.Where(kvp => kvp.Key.StartsWith(ownPrefix, StringComparison.Ordinal))
			.Select(kvp => kvp.Value);
	}

	private long CountRecordsForLocation(string tenantTerm, string tableName, string fieldName)
	{
		var count = 0L;
		foreach (var locations in ScopedLocations(tenantTerm))
		{
			lock (_locationsLock)
			{
				count += locations.Count(l => l.TableName == tableName && l.FieldName == fieldName);
			}
		}
		return count;
	}

	/// <summary>
	/// A registration together with the tenant term it was written under. The term is stored alongside the
	/// row rather than parsed back out of the key, so a change to the key format cannot silently change
	/// which rows a scoped read matches.
	/// </summary>
	private sealed record TenantScopedRegistration(string TenantId, DataLocationRegistration Registration);
}
