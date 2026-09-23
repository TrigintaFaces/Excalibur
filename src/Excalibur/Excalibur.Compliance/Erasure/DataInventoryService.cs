// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0




using Excalibur.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Implementation of <see cref="IDataInventoryService"/> for discovering and tracking
/// personal data locations for GDPR compliance.
/// </summary>
/// <remarks>
/// <para>
/// The data inventory service supports:
/// </para>
/// <list type="bullet">
/// <item><description>Registration-based discovery of actionable data locations (via <see cref="RegisterDataLocationAsync"/>)</description></item>
/// <item><description>Data mapping for GDPR RoPA (Records of Processing Activities)</description></item>
/// </list>
/// <para>
/// Inventory does NOT synthesize erase-able locations from <c>[PersonalData]</c> attributes (the attribute
/// carries no storage location). Instead, <c>[PersonalData]</c> annotations drive a <b>coverage gate</b>
/// An annotated category with no registered/discovered location forces a non-Completed erasure
/// certificate, so annotated personal data is never silently skipped. To make annotated data actionable,
/// register its location via <see cref="RegisterDataLocationAsync"/>.
/// </para>
/// </remarks>
public sealed partial class DataInventoryService : IDataInventoryService
{
	private readonly IDataInventoryStore _store;
	private readonly IDataInventoryQueryStore _queryStore;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IDataSubjectHasher _dataSubjectHasher;
	private readonly ILogger<DataInventoryService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataInventoryService"/> class.
	/// </summary>
	/// <param name="store">The data inventory store.</param>
	/// <param name="keyProvider">The key management provider.</param>
	/// <param name="dataSubjectHasher">The keyed hasher used to pseudonymize data-subject identifiers.</param>
	/// <param name="logger">The logger.</param>
	public DataInventoryService(
		IDataInventoryStore store,
		IKeyManagementProvider keyProvider,
		IDataSubjectHasher dataSubjectHasher,
		ILogger<DataInventoryService> logger)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_queryStore = (IDataInventoryQueryStore?)store.GetService(typeof(IDataInventoryQueryStore))
			?? throw new InvalidOperationException("The data inventory store does not support query operations.");
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_dataSubjectHasher = dataSubjectHasher ?? throw new ArgumentNullException(nameof(dataSubjectHasher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task<DataInventory> DiscoverAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataSubjectId);

		LogDataInventoryDiscoveryStarted(idType, tenantId);

		var locations = new List<DataLocation>();
		var keyReferences = new List<KeyReference>();

		// The DECLARED obligation. These are what a consumer registered as holding this subject's data,
		// and they are what coverage is judged against. They are deliberately NOT merged into `locations`:
		// a registration names the COLUMNS that hold the subject id and the key, while a location is a row
		// that was actually found, and turning one into the other would mean querying the consumer's own
		// schema. Carrying both at their honest granularity is what lets the certificate say something
		// true rather than something vacuous.
		//
		// READ THE WHOLE REGISTRY, NOT A SUBJECT-SCOPED SLICE. This previously called
		// FindRegistrationsForDataSubjectAsync(dataSubjectId, idType, ...), which refused every erasure in
		// every shipped configuration, and the reason is worth keeping because it is not obvious from
		// either side alone:
		//
		//   - the caller hardcodes DataSubjectIdType.Hash when it asks for discovery, while every shipped
		//     registration example registers a NATURAL id type (Email/UserId);
		//   - all three stores filter that query by id type and NEVER query the subject id itself
		//     (it is validated and discarded), so the "per-subject" read was never per-subject;
		//   - so the filter matched nothing, the declared set was empty for every subject, and the
		//     coverage gate downstream read that emptiness as "coverage unestablished".
		//
		// The declared set is TENANT-WIDE by construction. Reading it as such removes the id-type filter
		// that could only ever be wrong: passing status.IdType instead would assert something false about
		// a value that is a hash, and passing Hash asserts something false about the registry.
		// Reads through _store rather than _queryStore: GetAllRegistrationsAsync is on IDataInventoryStore,
		// and the narrower IDataInventoryQueryStore deliberately exposes only the subject-scoped reads. No
		// interface is widened for this.
		var registrations = await _store.GetAllRegistrationsAsync(cancellationToken)
			.ConfigureAwait(false);

		// Get previously discovered locations
		var discoveredLocations = await _queryStore.GetDiscoveredLocationsAsync(
			dataSubjectId,
			cancellationToken).ConfigureAwait(false);

		locations.AddRange(discoveredLocations);

		// Track unique keys
		var keyIds = new HashSet<string>();
		foreach (var loc in locations)
		{
			if (!string.IsNullOrEmpty(loc.KeyId))
			{
				_ = keyIds.Add(loc.KeyId);
			}
		}

		// Get key references
		foreach (var keyId in keyIds)
		{
			try
			{
				var keyInfo = await _keyProvider.GetKeyAsync(keyId, cancellationToken)
					.ConfigureAwait(false);

				if (keyInfo is not null)
				{
					keyReferences.Add(new KeyReference
					{
						KeyId = keyId,
						KeyScope = MapKeyScope(keyInfo.Purpose),
						EncryptedFieldValueCount = locations.Count(l => l.KeyId == keyId)
					});
				}
			}
			catch (Exception ex)
			{
				LogDataInventoryKeyInfoFailed(keyId, ex);
			}
		}

		var declaredLocations = registrations
			.Select(static r => new DataLocationKey(r.TableName, r.FieldName))
			.Distinct(DataLocationKey.Comparer)
			.ToList();

		// The category is carried ALONGSIDE the key rather than inside it. The key is a table-and-field
		// pair on purpose -- that is the granularity a contributor can state honestly -- so folding the
		// category in would stop the declared and discharged sides matching. The coverage gate's
		// annotated-category arm needs the categories themselves, and they were being discarded here.
		// The registered kind travels with the pair so the erasure service can offer a declared obligation
		// to the contributor that covers its store. Unclassified registrations are deliberately absent:
		// an Unknown kind is offered to nobody and therefore discharges nothing.
		var declaredLocationKinds = new Dictionary<DataLocationKey, DataStoreKind>(DataLocationKey.Comparer);
		foreach (var registration in registrations)
		{
			if (registration.StoreKind == DataStoreKind.Unknown)
			{
				continue;
			}

			declaredLocationKinds[new DataLocationKey(registration.TableName, registration.FieldName)]
				= registration.StoreKind;
		}

		var declaredCategories = registrations
			.Select(static r => r.DataCategory)
			.Where(static c => !string.IsNullOrWhiteSpace(c))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		var inventory = new DataInventory
		{
			DataSubjectId = _dataSubjectHasher.HashDataSubjectId(dataSubjectId),
			Locations = locations,
			DeclaredLocations = declaredLocations,
			DeclaredLocationKinds = declaredLocationKinds,
			DeclaredCategories = declaredCategories,
			AssociatedKeys = keyReferences,
			DiscoveredAt = DateTimeOffset.UtcNow
		};

		LogDataInventoryDiscoveryCompleted(locations.Count, keyReferences.Count);

		return inventory;
	}

	/// <inheritdoc />
	public async Task RegisterDataLocationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(registration);
		ValidateRegistration(registration);

		await _store.SaveRegistrationAsync(registration, cancellationToken).ConfigureAwait(false);

		LogDataInventoryRegistrationAdded(
				registration.TableName,
				registration.FieldName,
				registration.DataCategory);
	}

	/// <inheritdoc />
	public async Task UnregisterDataLocationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

		var removed = await _store.RemoveRegistrationAsync(tableName, fieldName, cancellationToken)
			.ConfigureAwait(false);

		if (removed)
		{
			LogDataInventoryRegistrationRemoved(tableName, fieldName);
		}
		else
		{
			LogDataInventoryRegistrationNotFound(tableName, fieldName);
		}
	}

	/// <inheritdoc />
	public async Task<DataMap> GetDataMapAsync(
		string? tenantId,
		CancellationToken cancellationToken)
	{
		var entries = await _queryStore.GetDataMapEntriesAsync(tenantId, cancellationToken)
			.ConfigureAwait(false);

		return new DataMap
		{
			Entries = entries,
			GeneratedAt = DateTimeOffset.UtcNow
		};
	}

	/// <summary>
	/// Records a discovered data location during automatic discovery.
	/// </summary>
	/// <param name="location">The discovered location.</param>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	internal async Task RecordDiscoveredLocationAsync(
		DataLocation location,
		string dataSubjectId,
		CancellationToken cancellationToken)
	{
		var hashedId = _dataSubjectHasher.HashDataSubjectId(dataSubjectId);
		await _store.RecordDiscoveredLocationAsync(location, hashedId, cancellationToken)
			.ConfigureAwait(false);
	}

	private static void ValidateRegistration(DataLocationRegistration registration)
	{
		if (string.IsNullOrWhiteSpace(registration.TableName))
		{
			throw new ArgumentException(Resources.DataInventoryService_TableNameRequired, nameof(registration));
		}

		if (string.IsNullOrWhiteSpace(registration.FieldName))
		{
			throw new ArgumentException(Resources.DataInventoryService_FieldNameRequired, nameof(registration));
		}

		if (string.IsNullOrWhiteSpace(registration.DataCategory))
		{
			throw new ArgumentException(Resources.DataInventoryService_DataCategoryRequired, nameof(registration));
		}

		if (string.IsNullOrWhiteSpace(registration.DataSubjectIdColumn))
		{
			throw new ArgumentException(Resources.DataInventoryService_DataSubjectIdColumnRequired, nameof(registration));
		}

		if (string.IsNullOrWhiteSpace(registration.KeyIdColumn))
		{
			throw new ArgumentException(Resources.DataInventoryService_KeyIdColumnRequired, nameof(registration));
		}
	}

	private static EncryptionKeyScope MapKeyScope(string? purpose)
	{
		return purpose?.ToUpperInvariant() switch
		{
			"USER" or "DEK" => EncryptionKeyScope.User,
			"TENANT" or "KEK" => EncryptionKeyScope.Tenant,
			"FIELD" => EncryptionKeyScope.Field,
			_ => EncryptionKeyScope.User
		};
	}

	[LoggerMessage(
			ComplianceEventId.DataInventoryDiscoveryStarted,
			LogLevel.Debug,
			"Discovering data inventory for data subject type {IdType}, tenant {TenantId}")]
	private partial void LogDataInventoryDiscoveryStarted(DataSubjectIdType idType, string? tenantId);

	[LoggerMessage(
			ComplianceEventId.DataInventoryKeyInfoFailed,
			LogLevel.Warning,
			"Failed to get key info for key {KeyId}")]
	private partial void LogDataInventoryKeyInfoFailed(string keyId, Exception exception);

	[LoggerMessage(
			ComplianceEventId.DataInventoryDiscoveryCompleted,
			LogLevel.Information,
			"Discovered {LocationCount} data locations and {KeyCount} encryption keys for data subject")]
	private partial void LogDataInventoryDiscoveryCompleted(int locationCount, int keyCount);

	[LoggerMessage(
			ComplianceEventId.DataInventoryRegistrationAdded,
			LogLevel.Information,
			"Registered data location {TableName}.{FieldName} for data category {Category}")]
	private partial void LogDataInventoryRegistrationAdded(string tableName, string fieldName, string category);

	[LoggerMessage(
			ComplianceEventId.DataInventoryRegistrationRemoved,
			LogLevel.Information,
			"Unregistered data location {TableName}.{FieldName}")]
	private partial void LogDataInventoryRegistrationRemoved(string tableName, string fieldName);

	[LoggerMessage(
			ComplianceEventId.DataInventoryRegistrationNotFound,
			LogLevel.Warning,
			"Data location {TableName}.{FieldName} not found for removal")]
	private partial void LogDataInventoryRegistrationNotFound(string tableName, string fieldName);
}
