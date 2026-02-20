// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Scope of encryption keys in the hierarchy.
/// </summary>
/// <remarks>
/// Renamed from KeyType to EncryptionKeyScope to avoid collision with Azure.Security.KeyVault.Keys.KeyType.
/// </remarks>
public enum EncryptionKeyScope
{
	/// <summary>
	/// User-specific key (DEK).
	/// </summary>
	User = 0,

	/// <summary>
	/// Tenant-level key (KEK).
	/// </summary>
	Tenant = 1,

	/// <summary>
	/// Field-specific key.
	/// </summary>
	Field = 2
}

/// <summary>
/// Service for discovering personal data associated with a data subject.
/// </summary>
/// <remarks>
/// The data inventory service supports:
/// - Automatic discovery from [PersonalData] attributed fields
/// - Manual registration for custom data locations
/// - Data mapping for GDPR RoPA (Records of Processing Activities)
/// </remarks>
public interface IDataInventoryService
{
	/// <summary>
	/// Discovers all personal data for a data subject.
	/// </summary>
	/// <param name="dataSubjectId">The data subject identifier.</param>
	/// <param name="idType">Type of the identifier.</param>
	/// <param name="tenantId">Optional tenant context.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Complete data inventory for the subject.</returns>
	Task<DataInventory> DiscoverAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Registers a manual data location for erasure.
	/// </summary>
	/// <param name="registration">The data location to register.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task RegisterDataLocationAsync(
		DataLocationRegistration registration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Removes a registered data location.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <param name="fieldName">The field name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task UnregisterDataLocationAsync(
		string tableName,
		string fieldName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the data map for compliance reporting (RoPA).
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Data map for RoPA reporting.</returns>
	Task<DataMap> GetDataMapAsync(
		string? tenantId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Complete inventory of personal data for a data subject.
/// </summary>
public sealed record DataInventory
{
	/// <summary>
	/// Gets the hashed data subject identifier (SHA-256 hex).
	/// </summary>
	/// <remarks>
	/// This value should be a SHA-256 hash of the original identifier
	/// to prevent storing raw PII in compliance tables.
	/// </remarks>
	public required string DataSubjectId { get; init; }

	/// <summary>
	/// Gets the discovered data locations.
	/// </summary>
	public IReadOnlyList<DataLocation> Locations { get; init; } = [];

	/// <summary>
	/// Gets the encryption keys associated with this data subject.
	/// </summary>
	public IReadOnlyList<KeyReference> AssociatedKeys { get; init; } = [];

	/// <summary>
	/// Gets the discovery timestamp.
	/// </summary>
	public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets whether discovery found any data.
	/// </summary>
	public bool HasData => Locations.Count > 0;

	/// <summary>
	/// Creates an empty inventory.
	/// </summary>
	public static DataInventory Empty(string dataSubjectId) =>
		new() { DataSubjectId = dataSubjectId };
}

/// <summary>
/// A location containing personal data.
/// </summary>
public sealed record DataLocation
{
	/// <summary>
	/// Gets the table or collection name.
	/// </summary>
	public required string TableName { get; init; }

	/// <summary>
	/// Gets the column or field name.
	/// </summary>
	public required string FieldName { get; init; }

	/// <summary>
	/// Gets the data category (from DataClassification).
	/// </summary>
	public required string DataCategory { get; init; }

	/// <summary>
	/// Gets the record identifier within the table.
	/// </summary>
	public required string RecordId { get; init; }

	/// <summary>
	/// Gets the encryption key ID used for this field.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets whether this is automatically discovered or manually registered.
	/// </summary>
	public bool IsAutoDiscovered { get; init; } = true;
}

/// <summary>
/// Reference to an encryption key.
/// </summary>
public sealed record KeyReference
{
	/// <summary>
	/// Gets the key identifier.
	/// </summary>
	public required string KeyId { get; init; }

	/// <summary>
	/// Gets the key scope (User, Tenant, Field).
	/// </summary>
	public required EncryptionKeyScope KeyScope { get; init; }

	/// <summary>
	/// Gets the number of records encrypted with this key.
	/// </summary>
	public int RecordCount { get; init; }
}

/// <summary>
/// Registration for a manual data location.
/// </summary>
public sealed record DataLocationRegistration
{
	/// <summary>
	/// Gets the table or collection name.
	/// </summary>
	public required string TableName { get; init; }

	/// <summary>
	/// Gets the column or field name.
	/// </summary>
	public required string FieldName { get; init; }

	/// <summary>
	/// Gets the data category.
	/// </summary>
	public required string DataCategory { get; init; }

	/// <summary>
	/// Gets the column containing the data subject identifier.
	/// </summary>
	public required string DataSubjectIdColumn { get; init; }

	/// <summary>
	/// Gets the type of identifier in the column.
	/// </summary>
	public required DataSubjectIdType IdType { get; init; }

	/// <summary>
	/// Gets the column containing the encryption key ID.
	/// </summary>
	public required string KeyIdColumn { get; init; }

	/// <summary>
	/// Gets an optional tenant ID column.
	/// </summary>
	public string? TenantIdColumn { get; init; }

	/// <summary>
	/// Gets a description of this data location.
	/// </summary>
	public string? Description { get; init; }
}

/// <summary>
/// Data map for GDPR RoPA (Records of Processing Activities).
/// </summary>
public sealed record DataMap
{
	/// <summary>
	/// Gets all registered data locations.
	/// </summary>
	public IReadOnlyList<DataMapEntry> Entries { get; init; } = [];

	/// <summary>
	/// Gets when the data map was generated.
	/// </summary>
	public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Entry in the data map.
/// </summary>
public sealed record DataMapEntry
{
	/// <summary>
	/// Gets the table name.
	/// </summary>
	public required string TableName { get; init; }

	/// <summary>
	/// Gets the field name.
	/// </summary>
	public required string FieldName { get; init; }

	/// <summary>
	/// Gets the data category.
	/// </summary>
	public required string DataCategory { get; init; }

	/// <summary>
	/// Gets whether this was auto-discovered.
	/// </summary>
	public bool IsAutoDiscovered { get; init; }

	/// <summary>
	/// Gets the total record count.
	/// </summary>
	public long RecordCount { get; init; }

	/// <summary>
	/// Gets the description.
	/// </summary>
	public string? Description { get; init; }
}
