// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Authorization;

/// <summary>
/// MongoDB BSON document representation of an activity group grant.
/// </summary>
internal sealed class ActivityGroupDocument
{
	/// <summary>
	/// Gets or sets the composite document ID (userId:tenantId:grantType:qualifier).
	/// </summary>
	/// <remarks>
	/// Uses a composite string ID based on business keys to ensure uniqueness and enable
	/// proper upsert behavior with ReplaceOneAsync.
	/// </remarks>
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the user/subject identifier.
	/// </summary>
	[BsonElement("userId")]
	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional display name.
	/// </summary>
	[BsonElement("fullName")]
	[BsonIgnoreIfNull]
	public string? FullName { get; set; }

	/// <summary>
	/// Gets or sets the tenant identifier.
	/// </summary>
	[BsonElement("tenantId")]
	[BsonIgnoreIfNull]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the grant type (e.g., activity-group).
	/// </summary>
	[BsonElement("grantType")]
	public string GrantType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the qualifier/activity group name.
	/// </summary>
	[BsonElement("qualifier")]
	public string Qualifier { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional expiration timestamp.
	/// </summary>
	[BsonElement("expiresOn")]
	[BsonIgnoreIfNull]
	public DateTimeOffset? ExpiresOn { get; set; }

	/// <summary>
	/// Gets or sets the identifier that issued the grant.
	/// </summary>
	[BsonElement("grantedBy")]
	public string GrantedBy { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the grant was created.
	/// </summary>
	[BsonElement("createdAt")]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the grant was last updated.
	/// </summary>
	[BsonElement("updatedAt")]
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Creates the composite document ID from business keys.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="tenantId">The tenant identifier (null converted to "null").</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The composite ID string.</returns>
	public static string CreateId(string userId, string? tenantId, string grantType, string qualifier) =>
		$"{userId}:{tenantId ?? "null"}:{grantType}:{qualifier}";
}
