// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.RequestProviders;

/// <summary>
/// Provides Postgres-specific implementations for queries related to activity groups.
/// </summary>
public sealed class PostgresActivityGroupRequestProvider
{
	/// <inheritdoc />
	public static IDataRequest<IDbConnection, bool> ActivityGroupExists(string activityGroupName, CancellationToken cancellationToken) =>
		new ExistsActivityGroupRequest(activityGroupName, cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, Dictionary<string, object>> FindActivityGroups(CancellationToken cancellationToken) =>
		new FindActivityGroupsRequest(cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> DeleteAllActivityGroups(CancellationToken cancellationToken) =>
		new DeleteAllActivityGroupsRequest(cancellationToken);

	/// <inheritdoc />
	public static IDataRequest<IDbConnection, int> CreateActivityGroup(string? tenantId, string name, string activityName,
		CancellationToken cancellationToken) => new CreateActivityGroupRequest(tenantId, name, activityName, cancellationToken);
}
