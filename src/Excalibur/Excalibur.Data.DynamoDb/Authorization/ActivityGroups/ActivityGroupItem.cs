// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2.Model;

namespace Excalibur.Data.DynamoDb.Authorization;

/// <summary>
/// DynamoDB item representation of an activity group grant.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key (PK) with "__null__" for null tenant values
/// to support efficient partition-based queries while maintaining consistency.
/// </para>
/// <para>
/// Uses a composite sort key (SK) with prefix: ACTGRP#{user_id}#{grant_type}#{qualifier}
/// to enable mixed entity types in a single table design.
/// </para>
/// </remarks>
internal static class ActivityGroupItem
{
	/// <summary>
	/// The value used for null tenant IDs in the partition key.
	/// </summary>
	internal const string NullTenantPartitionKey = "__null__";

	/// <summary>
	/// The prefix for activity group sort keys.
	/// </summary>
	private const string SortKeyPrefix = "ACTGRP#";

	// Attribute names
	private const string PkAttr = "tenant_id";

	private const string SkAttr = "sk";
	private const string UserIdAttr = "user_id";
	private const string FullNameAttr = "full_name";
	private const string OriginalTenantIdAttr = "original_tenant_id";
	private const string GrantTypeAttr = "grant_type";
	private const string QualifierAttr = "qualifier";
	private const string ExpiresOnAttr = "expires_on";
	private const string GrantedByAttr = "granted_by";
	private const string CreatedAtAttr = "created_at";
	private const string UpdatedAtAttr = "updated_at";
	private const string GsiUserIdAttr = "gsi_user_id";
	private const string GsiSkAttr = "gsi_sk";

	/// <summary>
	/// Gets the partition key attribute name.
	/// </summary>
	public static string PartitionKeyAttribute => PkAttr;

	/// <summary>
	/// Gets the sort key attribute name.
	/// </summary>
	public static string SortKeyAttribute => SkAttr;

	/// <summary>
	/// Gets the GSI user ID attribute name (partition key for GSI).
	/// </summary>
	public static string GsiUserIdAttribute => GsiUserIdAttr;

	/// <summary>
	/// Gets the GSI sort key attribute name.
	/// </summary>
	public static string GsiSortKeyAttribute => GsiSkAttr;

	/// <summary>
	/// Gets the grant type attribute name.
	/// </summary>
	public static string GrantTypeAttribute => GrantTypeAttr;

	/// <summary>
	/// Gets the user ID attribute name.
	/// </summary>
	public static string UserIdAttribute => UserIdAttr;

	/// <summary>
	/// Creates the partition key (tenant_id) for the given tenant ID.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <returns>The partition key value.</returns>
	public static string CreatePK(string? tenantId) =>
		tenantId ?? NullTenantPartitionKey;

	/// <summary>
	/// Creates the sort key for an activity group.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The sort key value.</returns>
	public static string CreateSK(string userId, string grantType, string qualifier) =>
		$"{SortKeyPrefix}{userId}#{grantType}#{qualifier}";

	/// <summary>
	/// Creates the GSI sort key for user-based queries.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The GSI sort key value.</returns>
	public static string CreateGsiSK(string? tenantId, string grantType, string qualifier) =>
		$"{tenantId ?? "null"}#ACTGRP#{grantType}#{qualifier}";

	/// <summary>
	/// Converts activity group data to a DynamoDB item.
	/// </summary>
	/// <param name="userId">The user identifier.</param>
	/// <param name="fullName">The display name.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <param name="expiresOn">The optional expiration timestamp.</param>
	/// <param name="grantedBy">The identifier that issued the grant.</param>
	/// <param name="createdAt">The creation timestamp.</param>
	/// <param name="updatedAt">The update timestamp.</param>
	/// <returns>A dictionary representing the DynamoDB item.</returns>
	public static Dictionary<string, AttributeValue> ToItem(
		string userId,
		string fullName,
		string? tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		DateTimeOffset createdAt,
		DateTimeOffset updatedAt)
	{
		var item = new Dictionary<string, AttributeValue>
		{
			[PkAttr] = new() { S = CreatePK(tenantId) },
			[SkAttr] = new() { S = CreateSK(userId, grantType, qualifier) },
			[UserIdAttr] = new() { S = userId },
			[FullNameAttr] = new() { S = fullName },
			[GrantTypeAttr] = new() { S = grantType },
			[QualifierAttr] = new() { S = qualifier },
			[GrantedByAttr] = new() { S = grantedBy },
			[CreatedAtAttr] = new() { S = createdAt.ToString("O") },
			[UpdatedAtAttr] = new() { S = updatedAt.ToString("O") },
			// GSI attributes for user-based queries
			[GsiUserIdAttr] = new() { S = userId },
			[GsiSkAttr] = new() { S = CreateGsiSK(tenantId, grantType, qualifier) }
		};

		if (tenantId is not null)
		{
			item[OriginalTenantIdAttr] = new() { S = tenantId };
		}

		if (expiresOn.HasValue)
		{
			item[ExpiresOnAttr] = new() { S = expiresOn.Value.ToString("O") };
		}

		return item;
	}

	/// <summary>
	/// Creates the key attributes for an activity group item.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="userId">The user identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The key attributes.</returns>
	public static Dictionary<string, AttributeValue> CreateKey(
		string? tenantId,
		string userId,
		string grantType,
		string qualifier) =>
		new() { [PkAttr] = new() { S = CreatePK(tenantId) }, [SkAttr] = new() { S = CreateSK(userId, grantType, qualifier) } };

	/// <summary>
	/// Gets the user ID from a DynamoDB item.
	/// </summary>
	/// <param name="item">The DynamoDB item.</param>
	/// <returns>The user ID.</returns>
	public static string GetUserId(Dictionary<string, AttributeValue> item) =>
		item[UserIdAttr].S;

	/// <summary>
	/// Gets the tenant ID from a DynamoDB item for deletion purposes.
	/// </summary>
	/// <param name="item">The DynamoDB item.</param>
	/// <returns>The partition key value (tenant_id).</returns>
	public static string GetTenantIdPK(Dictionary<string, AttributeValue> item) =>
		item[PkAttr].S;

	/// <summary>
	/// Gets the sort key from a DynamoDB item for deletion purposes.
	/// </summary>
	/// <param name="item">The DynamoDB item.</param>
	/// <returns>The sort key value.</returns>
	public static string GetSK(Dictionary<string, AttributeValue> item) =>
		item[SkAttr].S;
}
