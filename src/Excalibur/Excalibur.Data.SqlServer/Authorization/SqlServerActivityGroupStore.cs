// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.SqlServer.RequestProviders;

namespace Excalibur.Data.SqlServer.Authorization;

/// <summary>
/// SQL Server implementation of <see cref="IActivityGroupStore"/> that wraps existing
/// <see cref="SqlServerActivityGroupRequestProvider"/> request classes.
/// </summary>
public sealed class SqlServerActivityGroupStore : IActivityGroupStore
{
	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerActivityGroupStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	public SqlServerActivityGroupStore(IDomainDb domainDb)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public async Task<bool> ActivityGroupExistsAsync(string activityGroupName,
		CancellationToken cancellationToken)
	{
		var request = SqlServerActivityGroupRequestProvider.ActivityGroupExists(activityGroupName, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object>> FindActivityGroupsAsync(
		CancellationToken cancellationToken)
	{
		var request = SqlServerActivityGroupRequestProvider.FindActivityGroups(cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteAllActivityGroupsAsync(CancellationToken cancellationToken)
	{
		var request = SqlServerActivityGroupRequestProvider.DeleteAllActivityGroups(cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> CreateActivityGroupAsync(string? tenantId, string name,
		string activityName, CancellationToken cancellationToken)
	{
		var request = SqlServerActivityGroupRequestProvider.CreateActivityGroup(tenantId, name, activityName, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return null;
	}
}
