// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Helper class for Firestore grant document operations.
/// </summary>
/// <remarks>
/// <para>
/// Uses flat collections with composite document IDs:
/// {tenantId}_{userId}_{grantType}_{qualifier}
/// </para>
/// <para>
/// A grant always carries a tenant, so the tenant identifier appears verbatim in the
/// document ID and in the stored field.
/// </para>
/// </remarks>
internal static class FirestoreGrantDocument
{
	// Field names
	private const string TenantIdField = "tenant_id";

	private const string UserIdField = "user_id";
	private const string FullNameField = "full_name";
	private const string GrantTypeField = "grant_type";
	private const string QualifierField = "qualifier";
	private const string ExpiresOnField = "expires_on";
	private const string GrantedByField = "granted_by";
	private const string GrantedOnField = "granted_on";
	private const string IsRevokedField = "is_revoked";
	private const string RevokedByField = "revoked_by";
	private const string RevokedOnField = "revoked_on";

	/// <summary>
	/// Gets the tenant ID field name.
	/// </summary>
	public static string TenantIdFieldName => TenantIdField;

	/// <summary>
	/// Gets the user ID field name.
	/// </summary>
	public static string UserIdFieldName => UserIdField;

	/// <summary>
	/// Gets the grant type field name.
	/// </summary>
	public static string GrantTypeFieldName => GrantTypeField;

	/// <summary>
	/// Gets the qualifier field name.
	/// </summary>
	public static string QualifierFieldName => QualifierField;

	/// <summary>
	/// Gets the is revoked field name.
	/// </summary>
	public static string IsRevokedFieldName => IsRevokedField;

	/// <summary>
	/// Creates the document ID for a grant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="userId">The user identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The document ID.</returns>
	/// <remarks>
	/// <para>
	/// Every term is escaped before the join, so two distinct grants can never address one document. All four
	/// terms accept "_" as an ordinary character -- tenant slugs, user ids, grant types and qualifiers
	/// routinely contain it -- so joining them raw made ("a", "b_c") and ("a_b", "c") the same id, and the
	/// record written second silently replaced a different record written first.
	/// </para>
	/// <para>
	/// The constant "grant" term leads the id because Firestore rejects a document id matching
	/// <c>__.*__</c>. The reserved untenanted tenant term is <c>__untenanted__</c>, so without a leading
	/// constant every id in a deployment that does not use multi-tenancy starts with <c>__</c>, and a
	/// qualifier ending in <c>__</c> completes the pattern and fails the write.
	/// </para>
	/// </remarks>
	public static string CreateDocumentId(string tenantId, string userId, string grantType, string qualifier) =>
		FirestoreDocumentId.Compose(IdPrefix, tenantId, userId, grantType, qualifier);

	/// <summary>
	/// The constant leading term of every grants document id.
	/// </summary>
	internal const string IdPrefix = "grant";

	/// <summary>
	/// Converts a <see cref="Grant"/> to a Firestore document data dictionary.
	/// </summary>
	/// <param name="grant">The grant to convert.</param>
	/// <returns>A dictionary representing the Firestore document data.</returns>
	public static Dictionary<string, object> ToDocumentData(Grant grant)
	{
		ArgumentNullException.ThrowIfNull(grant);

		var data = new Dictionary<string, object>
		{
			[TenantIdField] = grant.TenantId,
			[UserIdField] = grant.UserId,
			[GrantTypeField] = grant.GrantType,
			[QualifierField] = grant.Qualifier,
			[GrantedByField] = grant.GrantedBy,
			[GrantedOnField] = Timestamp.FromDateTimeOffset(grant.GrantedOn),
			[IsRevokedField] = false
		};

		if (grant.FullName is not null)
		{
			data[FullNameField] = grant.FullName;
		}

		if (grant.ExpiresOn.HasValue)
		{
			data[ExpiresOnField] = Timestamp.FromDateTimeOffset(grant.ExpiresOn.Value);
		}

		return data;
	}

	/// <summary>
	/// Converts a Firestore document snapshot to a <see cref="Grant"/>.
	/// </summary>
	/// <param name="snapshot">The Firestore document snapshot.</param>
	/// <returns>A new grant instance, or null if the document is revoked or doesn't exist.</returns>
	public static Grant? FromSnapshot(DocumentSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		if (!snapshot.Exists)
		{
			return null;
		}

		// Check if revoked
		if (snapshot.TryGetValue<bool>(IsRevokedField, out var isRevoked) && isRevoked)
		{
			return null;
		}

		var userId = snapshot.GetValue<string>(UserIdField);
		var grantType = snapshot.GetValue<string>(GrantTypeField);
		var qualifier = snapshot.GetValue<string>(QualifierField);
		var grantedBy = snapshot.GetValue<string>(GrantedByField);
		var grantedOnTimestamp = snapshot.GetValue<Timestamp>(GrantedOnField);
		var grantedOn = grantedOnTimestamp.ToDateTimeOffset();

		string? fullName = null;
		if (snapshot.TryGetValue<string>(FullNameField, out var fn) && !string.IsNullOrEmpty(fn))
		{
			fullName = fn;
		}

		var tenantId = snapshot.GetValue<string>(TenantIdField);

		DateTimeOffset? expiresOn = null;
		if (snapshot.TryGetValue<Timestamp>(ExpiresOnField, out var expiresTimestamp))
		{
			expiresOn = expiresTimestamp.ToDateTimeOffset();
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
	/// Creates update data for marking a grant as revoked (soft delete).
	/// </summary>
	/// <param name="revokedBy">The identifier that revoked the grant.</param>
	/// <param name="revokedOn">The timestamp when the grant was revoked.</param>
	/// <returns>A dictionary containing the update data.</returns>
	public static Dictionary<string, object> CreateRevokeUpdate(string revokedBy, DateTimeOffset revokedOn) =>
		new() { [IsRevokedField] = true, [RevokedByField] = revokedBy, [RevokedOnField] = Timestamp.FromDateTimeOffset(revokedOn) };
}
