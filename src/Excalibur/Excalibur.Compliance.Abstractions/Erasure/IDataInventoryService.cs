// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

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
	/// Gets the table-and-field pairs a consumer REGISTERED as holding this subject's personal data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the obligation, and it is what coverage is judged against. It is deliberately coarser than
	/// <see cref="Locations"/>: a registration declares WHERE TO LOOK — a table and the columns that hold
	/// the subject identifier and the key — while a location is a row that was actually found. The two
	/// cannot be converted into one another without querying the consumer's own schema, so coverage is
	/// evaluated at the granularity both sides can honestly express.
	/// </para>
	/// <para>
	/// An erasure may be reported complete only when every declared pair was discharged by a contributor
	/// that reported doing so. An EMPTY declared set is not a clean bill of health: it means nothing was
	/// registered, so nothing could be enumerated, and a certificate issued over it would attest to an
	/// absence of evidence rather than to erasure.
	/// </para>
	/// </remarks>
	public IReadOnlyList<DataLocationKey> DeclaredLocations { get; init; } = [];

	/// <summary>
	/// Gets the store kind registered for each declared pair, for routing a pair to the contributor that
	/// covers its store. A pair absent from this map is <see cref="DataStoreKind.Unknown"/> and is offered
	/// to no contributor, so it stays outstanding.
	/// </summary>
	public IReadOnlyDictionary<DataLocationKey, DataStoreKind> DeclaredLocationKinds { get; init; }
		= new Dictionary<DataLocationKey, DataStoreKind>();

	/// <summary>
	/// Gets the data categories named by the registrations behind <see cref="DeclaredLocations"/>.
	/// </summary>
	/// <value>The declared categories, deduplicated, case-insensitively.</value>
	/// <remarks>
	/// <para>
	/// <b>Why this is a separate collection rather than a member of <see cref="DataLocationKey"/>.</b> The
	/// declared-versus-discharged gate matches on table-and-field pairs deliberately: that is the
	/// granularity a registration and a contributor can each state honestly, and a contributor reporting
	/// that it erased <c>Customers.Email</c> has no way to know which category a registration filed it
	/// under. Adding the category to the key would make the two sides stop matching and report a false
	/// outstanding obligation on every pair whose category the contributor could not name.
	/// </para>
	/// <para>
	/// <b>What it is for.</b> The annotated-but-undiscovered arm asks whether an annotated category is
	/// represented by any location. Before this existed it could see only DISCOVERED locations, so a
	/// category that had been registered but whose rows no contributor happened to find was reported as an
	/// uncovered annotation — the gate told the consumer to register something they had already registered.
	/// </para>
	/// </remarks>
	public IReadOnlyList<string> DeclaredCategories { get; init; } = [];

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
/// A table-and-field pair, the granularity at which an erasure obligation is declared and discharged.
/// </summary>
/// <param name="TableName">The table or collection name.</param>
/// <param name="FieldName">The column or field name.</param>
/// <remarks>
/// Comparison is ordinal and case-insensitive, because a consumer registering <c>Customers.Email</c> and
/// a contributor reporting <c>customers.email</c> have discharged the same obligation, and treating them
/// as different would report a false gap on every provider that normalises identifier case.
/// </remarks>
public readonly record struct DataLocationKey(string TableName, string FieldName)
{
	/// <summary>Gets a comparer that treats table and field names as case-insensitive.</summary>
	public static IEqualityComparer<DataLocationKey> Comparer { get; } = new CaseInsensitiveComparer();

	/// <inheritdoc />
	public override string ToString() => $"{TableName}.{FieldName}";

	private sealed class CaseInsensitiveComparer : IEqualityComparer<DataLocationKey>
	{
		public bool Equals(DataLocationKey x, DataLocationKey y) =>
			string.Equals(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(x.FieldName, y.FieldName, StringComparison.OrdinalIgnoreCase);

		public int GetHashCode(DataLocationKey obj) => HashCode.Combine(
			obj.TableName?.ToUpperInvariant(),
			obj.FieldName?.ToUpperInvariant());
	}
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
	/// Gets the kind of store that holds this personal data, used by the erasure coverage gate to route
	/// the location to its erasure mechanism (crypto-shred, a covering contributor, or an exemption).
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="DataStoreKind.Unknown"/>. An unclassified location is never coverable by a
	/// contributor or exemption — it can only be covered by crypto-shred (its <see cref="KeyId"/> being
	/// deleted) and otherwise forces a non-<c>Completed</c> erasure.
	/// </remarks>
	public DataStoreKind StoreKind { get; init; }

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
	/// Gets the number of encrypted field values this key protects.
	/// </summary>
	/// <remarks>
	/// This counts <b>occurrences</b>, not records: one unit is one (table, field, record) triple, so a
	/// key protecting three encrypted fields of a single record counts three. That is the quantity
	/// re-encryption planning needs. It is not a count of distinct data subjects or of rows.
	/// </remarks>
	public int EncryptedFieldValueCount { get; init; }
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
	/// Gets the kind of store that holds this registered location.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="DataStoreKind.Unknown"/>. An erasure contributor discharges a declared pair
	/// only when this kind is one it covers, so an unclassified registration discharges NOTHING and the
	/// erasure refuses to complete. That is deliberate: a registration whose store nobody claims is an
	/// obligation nobody has shown they erased, and silence must fail closed.
	/// </remarks>
	public DataStoreKind StoreKind { get; init; }

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
	/// <remarks>
	/// This is the NAME of a column in the consumer's own table — it records where that table keeps its
	/// tenant identifier. It does not associate this registration with a tenant and it never restricts
	/// which registrations a caller may read. Use <see cref="TenantId"/> for that; the two are easy to
	/// confuse and the confusion is what made data-inventory reads estate-wide while appearing scoped.
	/// </remarks>
	public string? TenantIdColumn { get; init; }

	/// <summary>
	/// Gets the tenant that owns this registration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant VALUE, as distinct from <see cref="TenantIdColumn"/>'s column name. This participates in
	/// the primary key of the stored registration, so two tenants registering the same table and field are
	/// two distinct rows rather than one overwriting the other.
	/// </para>
	/// <para>
	/// A registration that genuinely belongs to no tenant carries the reserved untenanted sentinel rather
	/// than <see langword="null"/> once stored: a nullable tenant makes "global" and "the caller forgot"
	/// indistinguishable, and the store cannot tell which one it is holding.
	/// </para>
	/// </remarks>
	public string? TenantId { get; init; }

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
	/// Gets the number of records holding this field, or <see langword="null"/> when the store did not
	/// count them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see langword="null"/> means <b>not counted</b>, and is the only honest answer for a store that
	/// cannot count. A value means counted, as of the moment the entry was produced. <b>A store that can
	/// count must count</b>: substituting a placeholder number is prohibited, because this value is
	/// reported in records of processing activity, where a reader takes it for a measurement.
	/// </para>
	/// <para>
	/// Consumers rendering a report must distinguish the two: an absent count is not zero, and totalling
	/// a column of counts across stores is only meaningful where every entry carries one.
	/// </para>
	/// </remarks>
	public long? RecordCount { get; init; }

	/// <summary>
	/// Gets the description.
	/// </summary>
	public string? Description { get; init; }
}
