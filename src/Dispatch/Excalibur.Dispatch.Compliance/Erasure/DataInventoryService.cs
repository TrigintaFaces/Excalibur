// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0




using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Implementation of <see cref="IDataInventoryService"/> for discovering and tracking
/// personal data locations for GDPR compliance.
/// </summary>
/// <remarks>
/// <para>
/// The data inventory service supports:
/// </para>
/// <list type="bullet">
/// <item><description>Automatic discovery from [PersonalData] attributed fields</description></item>
/// <item><description>Manual registration for custom data locations</description></item>
/// <item><description>Data mapping for GDPR RoPA (Records of Processing Activities)</description></item>
/// </list>
/// </remarks>
public sealed partial class DataInventoryService : IDataInventoryService
{
	private readonly IDataInventoryStore _store;
	private readonly IDataInventoryQueryStore _queryStore;
	private readonly IKeyManagementProvider _keyProvider;
	private readonly ILogger<DataInventoryService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataInventoryService"/> class.
	/// </summary>
	/// <param name="store">The data inventory store.</param>
	/// <param name="keyProvider">The key management provider.</param>
	/// <param name="logger">The logger.</param>
	public DataInventoryService(
		IDataInventoryStore store,
		IKeyManagementProvider keyProvider,
		ILogger<DataInventoryService> logger)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_queryStore = (IDataInventoryQueryStore?)store.GetService(typeof(IDataInventoryQueryStore))
			?? throw new InvalidOperationException("The data inventory store does not support query operations.");
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
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

		// Get registered data locations that might contain this data subject
		var registrations = await _queryStore.FindRegistrationsForDataSubjectAsync(
			dataSubjectId,
			idType,
			tenantId,
			cancellationToken).ConfigureAwait(false);

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
						RecordCount = locations.Count(l => l.KeyId == keyId)
					});
				}
			}
			catch (Exception ex)
			{
				LogDataInventoryKeyInfoFailed(keyId, ex);
			}
		}

		var inventory = new DataInventory
		{
			DataSubjectId = DataSubjectHasher.HashDataSubjectId(dataSubjectId),
			Locations = locations,
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
		var hashedId = DataSubjectHasher.HashDataSubjectId(dataSubjectId);
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
