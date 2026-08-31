// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.MaterializedViews;

/// <summary>
/// MongoDB document model for materialized views.
/// </summary>
/// <remarks>
/// Uses a composite key (tenant + view_name + view_id) for the document ID, which both enables efficient
/// lookups and confines each document to its tenant's partition. Without the tenant segment two tenants
/// projecting the same named view addressed ONE document, so the later writer's data silently replaced the
/// earlier one's and a read returned whichever tenant wrote last.
/// </remarks>
internal sealed class MongoDbMaterializedViewDocument
{
	/// <summary>
	/// Gets or sets the unique document ID (composite: viewName:viewId).
	/// </summary>
	[BsonId]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the owning tenant. The identifier already confines the document to its partition; this
	/// field makes that partition visible to a query and to an operator reading the collection.
	/// </summary>
	[BsonElement("tenant_id")]
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the view name (type discriminator).
	/// </summary>
	[BsonElement("view_name")]
	public string ViewName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the view instance ID.
	/// </summary>
	[BsonElement("view_id")]
	public string ViewId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized view data as BSON.
	/// </summary>
	[BsonElement("data")]
	public BsonDocument Data { get; set; } = new();

	/// <summary>
	/// Gets or sets when the document was created.
	/// </summary>
	[BsonElement("created_at")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the document was last updated.
	/// </summary>
	[BsonElement("updated_at")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Creates a composite document ID from the owning tenant, the view name and the view ID.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant segment is length-prefixed rather than merely delimited. A tenant identifier may legally
	/// contain the delimiter, and without the prefix ("a", "b:c") and ("a:b", "c") compose to the SAME
	/// identifier -- a cross-tenant collision reintroduced by the very code meant to prevent one. The prefix
	/// makes the segment self-delimiting, so no two distinct tenants can produce the same identifier.
	/// </para>
	/// <para>
	/// The term is always present. The caller resolves it through <c>KeyedTenantPartition</c>, which has no
	/// empty inhabitant, so an unscoped host binds the reserved untenanted sentinel rather than omitting the
	/// segment: "this deployment has no tenants" and "somebody forgot to supply one" cannot become the same
	/// document.
	/// </para>
	/// </remarks>
	/// <param name="tenantId">The owning tenant, as resolved from the store's ambient context.</param>
	/// <param name="viewName">The view name.</param>
	/// <param name="viewId">The view ID.</param>
	/// <returns>The composite document ID.</returns>
	public static string CreateId(string tenantId, string viewName, string viewId) =>
		string.Create(CultureInfo.InvariantCulture, $"t{tenantId.Length}:{tenantId}:{viewName}:{viewId}");

	/// <summary>
	/// Creates the checkpoint document ID for a view, confined to the owning tenant.
	/// </summary>
	/// <remarks>
	/// Keyed on view name alone this collection held ONE checkpoint for every tenant, so one tenant's
	/// progress advanced another's and that tenant's projector skipped every event in between -- silently,
	/// and permanently, because the monotonic advance exists to stop the checkpoint moving backwards.
	/// See <see cref="CreateId(string, string, string)"/> for why the tenant segment is length-prefixed.
	/// </remarks>
	/// <param name="tenantId">The owning tenant, as resolved from the store's ambient context.</param>
	/// <param name="viewName">The view name.</param>
	/// <returns>The checkpoint document ID.</returns>
	public static string CreatePositionId(string tenantId, string viewName) =>
		string.Create(CultureInfo.InvariantCulture, $"t{tenantId.Length}:{tenantId}:{viewName}");
}

/// <summary>
/// MongoDB document model for materialized view position tracking.
/// </summary>
internal sealed class MongoDbMaterializedViewPositionDocument
{
	/// <summary>
	/// Gets or sets the unique document ID (view name).
	/// </summary>
	[BsonId]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the owning tenant. The identifier already confines the checkpoint to its partition; this
	/// field makes that partition visible to a query and to an operator reading the collection.
	/// </summary>
	[BsonElement("tenant_id")]
	public string TenantId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the view name.
	/// </summary>
	[BsonElement("view_name")]
	public string ViewName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current position in the event stream.
	/// </summary>
	[BsonElement("position")]
	public long Position { get; set; }

	/// <summary>
	/// Gets or sets when the document was created.
	/// </summary>
	[BsonElement("created_at")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the position was last updated.
	/// </summary>
	[BsonElement("updated_at")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTimeOffset UpdatedAt { get; set; }
}
