// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Authorization;

/// <summary>
/// MongoDB BSON document representation of an authorization grant.
/// </summary>
internal sealed class GrantDocument
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
	/// Gets or sets the grant type (e.g., role, activity-group).
	/// </summary>
	[BsonElement("grantType")]
	public string GrantType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the qualifier/scope for the grant.
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
	/// Gets or sets the timestamp when the grant was issued.
	/// </summary>
	[BsonElement("grantedOn")]
	public DateTimeOffset GrantedOn { get; set; }

	/// <summary>
	/// Gets or sets whether the grant is revoked.
	/// </summary>
	[BsonElement("isRevoked")]
	public bool IsRevoked { get; set; }

	/// <summary>
	/// Gets or sets the identifier that revoked the grant.
	/// </summary>
	[BsonElement("revokedBy")]
	[BsonIgnoreIfNull]
	public string? RevokedBy { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the grant was revoked.
	/// </summary>
	[BsonElement("revokedOn")]
	[BsonIgnoreIfNull]
	public DateTimeOffset? RevokedOn { get; set; }

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

	/// <summary>
	/// Converts a <see cref="Grant"/> to a <see cref="GrantDocument"/>.
	/// </summary>
	/// <param name="grant">The grant to convert.</param>
	/// <returns>A new document instance.</returns>
	public static GrantDocument FromGrant(Grant grant)
	{
		ArgumentNullException.ThrowIfNull(grant);

		return new GrantDocument
		{
			Id = CreateId(grant.UserId, grant.TenantId, grant.GrantType, grant.Qualifier),
			UserId = grant.UserId,
			FullName = grant.FullName,
			TenantId = grant.TenantId,
			GrantType = grant.GrantType,
			Qualifier = grant.Qualifier,
			ExpiresOn = grant.ExpiresOn,
			GrantedBy = grant.GrantedBy,
			GrantedOn = grant.GrantedOn,
			IsRevoked = false
		};
	}

	/// <summary>
	/// Converts this document to a <see cref="Grant"/>.
	/// </summary>
	/// <returns>A new grant instance.</returns>
	public Grant ToGrant()
	{
		return new Grant(
			UserId,
			FullName,
			TenantId,
			GrantType,
			Qualifier,
			ExpiresOn,
			GrantedBy,
			GrantedOn);
	}
}
