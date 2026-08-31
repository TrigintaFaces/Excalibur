// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Saga.MongoDB;

/// <summary>
/// MongoDB document representation of saga state.
/// </summary>
/// <remarks>
/// <para>
/// The document key is the owning tenant composed with the saga identifier, not the saga identifier
/// alone. Sagas are correlated by a business key such as an order identifier, so two tenants
/// legitimately hold the same saga identifier; keyed on that identifier alone they are one document,
/// and the second tenant's create collides with the first tenant's document instead of making its own.
/// The Guid is stored with string representation for readability and portability.
/// </para>
/// <para>
/// Following MongoDB naming conventions with camelCase element names.
/// </para>
/// </remarks>
internal sealed class MongoDbSagaDocument
{
	/// <summary>
	/// Gets or sets the stored document key (_id): the owning tenant composed with the saga identifier.
	/// </summary>
	/// <remarks>
	/// The tenant is part of the document's IDENTITY, which is a different property from the tenant term a
	/// query carries. A term scopes which documents a query is willing to return; the identity decides which
	/// documents can exist at all. With the tenant only in the term, both tenants still address one document,
	/// so the term can refuse a cross-tenant overwrite but cannot let the second tenant create its own saga —
	/// the refusal degenerates into an estate-wide uniqueness constraint on the saga identifier. With the
	/// tenant in the key, each tenant has its own document and a cross-tenant write is unaddressable rather
	/// than merely refused.
	/// </remarks>
	[BsonId]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga identifier as the caller supplied it, without the tenant segment.
	/// </summary>
	/// <remarks>
	/// Persisted beside <see cref="Id"/> rather than parsed back out of it, so the identifier a caller
	/// asked for is returned unchanged and no reader has to know the key's composition to recover it.
	/// </remarks>
	[BsonElement("sagaId")]
	[BsonRepresentation(BsonType.String)]
	public Guid SagaId { get; set; }

	/// <summary>
	/// Gets or sets the saga type name.
	/// </summary>
	[BsonElement("sagaType")]
	public string SagaType { get; set; } = string.Empty;

	/// <summary>
	/// The tenant that owns this saga, or <see langword="null"/> for the untenanted partition.
	/// </summary>
	/// <remarks>
	/// A FIRST-CLASS document field, deliberately, and not read out of <see cref="StateJson"/>. The state is
	/// persisted as an opaque serialized string, so a tenant living only inside it cannot appear in a server-side
	/// filter — the store would have to fetch a document before it could discover whose it was, which is the
	/// leak rather than a defence against it. Promoting it here is what makes every keyed read and write
	/// tenant-filterable in the query itself.
	/// </remarks>
	[BsonElement("tenantId")]
	[BsonIgnoreIfNull]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the JSON-serialized saga state.
	/// </summary>
	[BsonElement("stateJson")]
	public string StateJson { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether the saga is completed.
	/// </summary>
	[BsonElement("isCompleted")]
	public bool IsCompleted { get; set; }

	/// <summary>
	/// Gets or sets the explicit completion instant (UTC) the retention purge keys on. Stored as a nullable
	/// <see cref="DateTime"/> (UTC) rather than <see cref="DateTimeOffset"/> because the MongoDB LINQ provider
	/// cannot translate <see cref="DateTimeOffset"/> comparisons; <see langword="null"/> until the saga completes.
	/// </summary>
	[BsonElement("completedAt")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the optimistic-concurrency version of the persisted saga state.
	/// </summary>
	[BsonElement("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets when the document was created.
	/// </summary>
	[BsonElement("createdUtc")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime CreatedUtc { get; set; }

	/// <summary>
	/// Gets or sets when the document was last updated.
	/// </summary>
	[BsonElement("updatedUtc")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime UpdatedUtc { get; set; }
}
