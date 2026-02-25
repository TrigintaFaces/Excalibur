// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.MaterializedViews;

/// <summary>
/// MongoDB document model for materialized views.
/// </summary>
/// <remarks>
/// Uses a composite key (view_name + view_id) for the document ID
/// to enable efficient lookups and ensure uniqueness within a single collection.
/// </remarks>
internal sealed class MongoDbMaterializedViewDocument
{
	/// <summary>
	/// Gets or sets the unique document ID (composite: viewName:viewId).
	/// </summary>
	[BsonId]
	public string Id { get; set; } = string.Empty;

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
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the document was last updated.
	/// </summary>
	[BsonElement("updated_at")]
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Creates a composite document ID from view name and view ID.
	/// </summary>
	/// <param name="viewName">The view name.</param>
	/// <param name="viewId">The view ID.</param>
	/// <returns>The composite document ID.</returns>
	public static string CreateId(string viewName, string viewId) => $"{viewName}:{viewId}";
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
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets when the position was last updated.
	/// </summary>
	[BsonElement("updated_at")]
	public DateTimeOffset UpdatedAt { get; set; }
}
