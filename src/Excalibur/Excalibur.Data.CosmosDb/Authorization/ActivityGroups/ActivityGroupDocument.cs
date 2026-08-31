// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Cosmos DB document representation of an activity group grant.
/// </summary>
/// <remarks>
/// Uses tenant_id as the partition key
/// to support efficient partition-based queries while maintaining consistency.
/// </remarks>
internal sealed class ActivityGroupDocument
{
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
	/// This is the tenant identifier verbatim; a grant always carries one.
	/// </remarks>
	[JsonPropertyName("tenant_id")]
	public string TenantId { get; set; } = string.Empty;

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
	/// Gets or sets the grant type (e.g., activity-group).
	/// </summary>
	[JsonPropertyName("grant_type")]
	public string GrantType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the qualifier/activity group name.
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
	/// Gets or sets the timestamp when the grant was created.
	/// </summary>
	[JsonPropertyName("created_at")]
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the grant was last updated.
	/// </summary>
	[JsonPropertyName("updated_at")]
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Creates the composite document ID from business keys.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="tenantId">The tenant identifier (null converted to "null").</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The composite ID string.</returns>
	public static string CreateId(string userId, string tenantId, string grantType, string qualifier) =>
		$"{userId}:{tenantId}:{grantType}:{qualifier}";
}
