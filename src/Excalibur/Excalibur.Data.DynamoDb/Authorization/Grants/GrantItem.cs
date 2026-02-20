// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.DynamoDBv2.Model;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Data.DynamoDb.Authorization;

/// <summary>
/// DynamoDB item representation of an authorization grant.
/// </summary>
/// <remarks>
/// <para>
/// Uses tenant_id as the partition key (PK) with "__null__" for null tenant values
/// to support efficient partition-based queries while maintaining consistency.
/// </para>
/// <para>
/// Uses a composite sort key (SK) with prefix: GRANT#{user_id}#{grant_type}#{qualifier}
/// to enable mixed entity types in a single table design.
/// </para>
/// </remarks>
internal static class GrantItem
{
	/// <summary>
	/// The value used for null tenant IDs in the partition key.
	/// </summary>
	internal const string NullTenantPartitionKey = "__null__";

	/// <summary>
	/// The prefix for grant sort keys.
	/// </summary>
	private const string SortKeyPrefix = "GRANT#";

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
	private const string GrantedOnAttr = "granted_on";
	private const string IsRevokedAttr = "is_revoked";
	private const string RevokedByAttr = "revoked_by";
	private const string RevokedOnAttr = "revoked_on";
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
	/// Gets the is revoked attribute name.
	/// </summary>
	public static string IsRevokedAttribute => IsRevokedAttr;

	/// <summary>
	/// Gets the grant type attribute name.
	/// </summary>
	public static string GrantTypeAttribute => GrantTypeAttr;

	/// <summary>
	/// Gets the qualifier attribute name.
	/// </summary>
	public static string QualifierAttribute => QualifierAttr;

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
	/// Creates the sort key for a grant.
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
		$"{tenantId ?? "null"}#GRANT#{grantType}#{qualifier}";

	/// <summary>
	/// Converts a <see cref="Grant"/> to a DynamoDB item.
	/// </summary>
	/// <param name="grant">The grant to convert.</param>
	/// <returns>A dictionary representing the DynamoDB item.</returns>
	public static Dictionary<string, AttributeValue> ToItem(Grant grant)
	{
		ArgumentNullException.ThrowIfNull(grant);

		var item = new Dictionary<string, AttributeValue>
		{
			[PkAttr] = new() { S = CreatePK(grant.TenantId) },
			[SkAttr] = new() { S = CreateSK(grant.UserId, grant.GrantType, grant.Qualifier) },
			[UserIdAttr] = new() { S = grant.UserId },
			[GrantTypeAttr] = new() { S = grant.GrantType },
			[QualifierAttr] = new() { S = grant.Qualifier },
			[GrantedByAttr] = new() { S = grant.GrantedBy },
			[GrantedOnAttr] = new() { S = grant.GrantedOn.ToString("O") },
			[IsRevokedAttr] = new() { BOOL = false },
			// GSI attributes for user-based queries
			[GsiUserIdAttr] = new() { S = grant.UserId },
			[GsiSkAttr] = new() { S = CreateGsiSK(grant.TenantId, grant.GrantType, grant.Qualifier) }
		};

		if (grant.FullName is not null)
		{
			item[FullNameAttr] = new() { S = grant.FullName };
		}

		if (grant.TenantId is not null)
		{
			item[OriginalTenantIdAttr] = new() { S = grant.TenantId };
		}

		if (grant.ExpiresOn.HasValue)
		{
			item[ExpiresOnAttr] = new() { S = grant.ExpiresOn.Value.ToString("O") };
		}

		return item;
	}

	/// <summary>
	/// Converts a DynamoDB item to a <see cref="Grant"/>.
	/// </summary>
	/// <param name="item">The DynamoDB item.</param>
	/// <returns>A new grant instance, or null if the item is revoked.</returns>
	public static Grant? FromItem(Dictionary<string, AttributeValue> item)
	{
		ArgumentNullException.ThrowIfNull(item);

		// Check if revoked
		if (item.TryGetValue(IsRevokedAttr, out var isRevokedAttr) &&
			isRevokedAttr.BOOL == true)
		{
			return null;
		}

		var userId = item[UserIdAttr].S;
		var grantType = item[GrantTypeAttr].S;
		var qualifier = item[QualifierAttr].S;
		var grantedBy = item[GrantedByAttr].S;
		var grantedOn = DateTimeOffset.Parse(item[GrantedOnAttr].S, CultureInfo.InvariantCulture);

		string? fullName = null;
		if (item.TryGetValue(FullNameAttr, out var fullNameAttr) && !string.IsNullOrEmpty(fullNameAttr.S))
		{
			fullName = fullNameAttr.S;
		}

		string? tenantId = null;
		if (item.TryGetValue(OriginalTenantIdAttr, out var tenantAttr) && !string.IsNullOrEmpty(tenantAttr.S))
		{
			tenantId = tenantAttr.S;
		}

		DateTimeOffset? expiresOn = null;
		if (item.TryGetValue(ExpiresOnAttr, out var expiresAttr) && !string.IsNullOrEmpty(expiresAttr.S))
		{
			expiresOn = DateTimeOffset.Parse(expiresAttr.S, CultureInfo.InvariantCulture);
		}

		return new Grant(
			userId,
			fullName,
			tenantId,
			grantType,
			qualifier,
			expiresOn,
			grantedBy,
			grantedOn);
	}

	/// <summary>
	/// Creates the key attributes for a grant item.
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
	/// Creates update expression items for marking a grant as revoked (soft delete).
	/// </summary>
	/// <param name="revokedBy">The identifier that revoked the grant.</param>
	/// <param name="revokedOn">The timestamp when the grant was revoked.</param>
	/// <returns>A tuple containing the update expression and attribute values.</returns>
	public static (string UpdateExpression, Dictionary<string, AttributeValue> ExpressionValues) CreateRevokeUpdate(
		string revokedBy,
		DateTimeOffset revokedOn) =>
	(
		$"SET {IsRevokedAttr} = :revoked, {RevokedByAttr} = :revokedBy, {RevokedOnAttr} = :revokedOn",
		new Dictionary<string, AttributeValue>
		{
			[":revoked"] = new() { BOOL = true },
			[":revokedBy"] = new() { S = revokedBy },
			[":revokedOn"] = new() { S = revokedOn.ToString("O") }
		}
	);
}
