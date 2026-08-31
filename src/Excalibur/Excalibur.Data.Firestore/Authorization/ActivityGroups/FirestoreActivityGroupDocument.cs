// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Helper class for Firestore activity group document operations.
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
internal static class FirestoreActivityGroupDocument
{
	// Field names
	private const string TenantIdField = "tenant_id";

	private const string UserIdField = "user_id";
	private const string FullNameField = "full_name";
	private const string GrantTypeField = "grant_type";
	private const string QualifierField = "qualifier";
	private const string ExpiresOnField = "expires_on";
	private const string GrantedByField = "granted_by";
	private const string CreatedAtField = "created_at";
	private const string UpdatedAtField = "updated_at";

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
	/// Creates the document ID for an activity group grant.
	/// </summary>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="userId">The user identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier.</param>
	/// <returns>The document ID.</returns>
	/// <remarks>
	/// <para>
	/// Every term is escaped before the join, so two distinct activity-group grants can never address one document. All four
	/// terms accept "_" as an ordinary character -- tenant slugs, user ids, grant types and qualifiers
	/// routinely contain it -- so joining them raw made ("a", "b_c") and ("a_b", "c") the same id, and the
	/// record written second silently replaced a different record written first.
	/// </para>
	/// <para>
	/// The constant "ag" term leads the id because Firestore rejects a document id matching
	/// <c>__.*__</c>. The reserved untenanted tenant term is <c>__untenanted__</c>, so without a leading
	/// constant every id in a deployment that does not use multi-tenancy starts with <c>__</c>, and a
	/// qualifier ending in <c>__</c> completes the pattern and fails the write.
	/// </para>
	/// </remarks>
	public static string CreateDocumentId(string tenantId, string userId, string grantType, string qualifier) =>
		FirestoreDocumentId.Compose(IdPrefix, tenantId, userId, grantType, qualifier);

	/// <summary>
	/// The constant leading term of every activity-group grants document id.
	/// </summary>
	internal const string IdPrefix = "ag";

	/// <summary>
	/// Converts activity group data to a Firestore document data dictionary.
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
	/// <returns>A dictionary representing the Firestore document data.</returns>
	public static Dictionary<string, object> ToDocumentData(
		string userId,
		string fullName,
		string tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		DateTimeOffset createdAt,
		DateTimeOffset updatedAt)
	{
		var data = new Dictionary<string, object>
		{
			[TenantIdField] = tenantId,
			[UserIdField] = userId,
			[FullNameField] = fullName,
			[GrantTypeField] = grantType,
			[QualifierField] = qualifier,
			[GrantedByField] = grantedBy,
			[CreatedAtField] = Timestamp.FromDateTimeOffset(createdAt),
			[UpdatedAtField] = Timestamp.FromDateTimeOffset(updatedAt)
		};

		if (expiresOn.HasValue)
		{
			data[ExpiresOnField] = Timestamp.FromDateTimeOffset(expiresOn.Value);
		}

		return data;
	}

	/// <summary>
	/// Gets the user ID from a Firestore document snapshot.
	/// </summary>
	/// <param name="snapshot">The Firestore document snapshot.</param>
	/// <returns>The user ID.</returns>
	public static string GetUserId(DocumentSnapshot snapshot) =>
		snapshot.GetValue<string>(UserIdField);
}
