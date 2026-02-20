// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// A query provider for Postgres databases, implementing the <see cref="IGrantRequestProvider" /> interface.
/// </summary>
public sealed class PostgresGrantRequestProvider
{
	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> DeleteGrant(string userId, string tenantId, string grantType, string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn, CancellationToken cancellationToken) =>
		new DeleteGrantRequest(userId, tenantId, grantType, qualifier, revokedBy, revokedOn, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, bool> GrantExists(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ExistsGrantRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, IEnumerable<Grant>> GetMatchingGrants(string? userId, string tenantId, string grantType,
		string qualifier,
		CancellationToken cancellationToken) =>
		new MatchingGrantsRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, Grant?> GetGrant(string userId, string tenantId, string grantType, string qualifier,
		CancellationToken cancellationToken) =>
		new ReadGrantRequest(userId, tenantId, grantType, qualifier, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, IEnumerable<Grant>> GetAllGrants(string userId, CancellationToken cancellationToken) =>
		new ReadAllGrantsRequest(userId, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> SaveGrant(Grant grant, CancellationToken cancellationToken) =>
		new SaveGrantRequest(grant, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, Dictionary<string, object>> FindUserGrants(string userId, CancellationToken cancellationToken) =>
		new FindUserGrantsRequest(userId, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> DeleteActivityGroupGrantsByUserId(string userId, string grantType,
		CancellationToken cancellationToken) =>
		new DeleteActivityGroupGrantsByUserIdRequest(userId, grantType, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> DeleteAllActivityGroupGrants(string grantType, CancellationToken cancellationToken) =>
		new DeleteAllActivityGroupGrantsRequest(grantType, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> InsertActivityGroupGrant(string userId, string fullName, string? tenantId, string grantType,
		string qualifier, DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken) =>
		new InsertActivityGroupGrantRequest(userId, fullName, tenantId, grantType, qualifier, expiresOn, grantedBy, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, IEnumerable<string>> GetDistinctActivityGroupGrantUserIds(
		string grantType,
		CancellationToken cancellationToken) => new GetDistinctActivityGroupGrantUserIdsRequest(grantType, cancellationToken);
}
