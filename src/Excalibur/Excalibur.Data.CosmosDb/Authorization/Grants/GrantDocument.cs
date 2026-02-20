// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Cosmos DB document representation of an authorization grant.
/// </summary>
/// <remarks>
/// Uses tenant_id as the partition key with "__null__" for null tenant values
/// to support efficient partition-based queries while maintaining consistency.
/// </remarks>
internal sealed class GrantDocument
{
	/// <summary>
	/// The value used for null tenant IDs in the partition key.
	/// </summary>
	internal const string NullTenantPartitionKey = "__null__";

	/// <summary>
	/// Gets or sets the composite document ID (userId:tenantId:grantType:qualifier).
	/// </summary>
	/// <remarks>
	/// Uses a composite string ID based on business keys to ensure uniqueness and enable
	/// proper upsert behavior with UpsertItemAsync.
	/// </remarks>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the tenant ID used as partition key.
	/// </summary>
	/// <remarks>
	/// Uses "__null__" for null tenant values to enable partition key-based queries.
	/// </remarks>
	[JsonPropertyName("tenant_id")]
	public string TenantId { get; set; } = NullTenantPartitionKey;

	/// <summary>
	/// Gets or sets the original tenant ID (null if no tenant).
	/// </summary>
	[JsonPropertyName("original_tenant_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? OriginalTenantId { get; set; }

	/// <summary>
	/// Gets or sets the user/subject identifier.
	/// </summary>
	[JsonPropertyName("user_id")]
	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional display name.
	/// </summary>
	[JsonPropertyName("full_name")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? FullName { get; set; }

	/// <summary>
	/// Gets or sets the grant type (e.g., role, activity-group).
	/// </summary>
	[JsonPropertyName("grant_type")]
	public string GrantType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the qualifier/scope for the grant.
	/// </summary>
	[JsonPropertyName("qualifier")]
	public string Qualifier { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the optional expiration timestamp.
	/// </summary>
	[JsonPropertyName("expires_on")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public DateTimeOffset? ExpiresOn { get; set; }

	/// <summary>
	/// Gets or sets the identifier that issued the grant.
	/// </summary>
	[JsonPropertyName("granted_by")]
	public string GrantedBy { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the grant was issued.
	/// </summary>
	[JsonPropertyName("granted_on")]
	public DateTimeOffset GrantedOn { get; set; }

	/// <summary>
	/// Gets or sets whether the grant is revoked.
	/// </summary>
	[JsonPropertyName("is_revoked")]
	public bool IsRevoked { get; set; }

	/// <summary>
	/// Gets or sets the identifier that revoked the grant.
	/// </summary>
	[JsonPropertyName("revoked_by")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? RevokedBy { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the grant was revoked.
	/// </summary>
	[JsonPropertyName("revoked_on")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
	/// Gets the partition key value for the given tenant ID.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The partition key value.</returns>
	public static string GetPartitionKey(string? tenantId) =>
		tenantId ?? NullTenantPartitionKey;

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
			TenantId = GetPartitionKey(grant.TenantId),
			OriginalTenantId = grant.TenantId,
			UserId = grant.UserId,
			FullName = grant.FullName,
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
			OriginalTenantId,
			GrantType,
			Qualifier,
			ExpiresOn,
			GrantedBy,
			GrantedOn);
	}
}
