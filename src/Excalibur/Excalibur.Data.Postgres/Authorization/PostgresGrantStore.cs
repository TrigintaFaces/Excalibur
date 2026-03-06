// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.Postgres.RequestProviders;

namespace Excalibur.Data.Postgres.Authorization;

/// <summary>
/// PostgreSQL implementation of <see cref="IGrantStore"/> that wraps existing
/// <see cref="PostgresGrantRequestProvider"/> request classes.
/// </summary>
/// <remarks>
/// Implements both <see cref="IGrantStore"/> and <see cref="IGrantQueryStore"/> (via <see cref="GetService"/>)
/// plus <see cref="IActivityGroupGrantStore"/> for activity-group grant operations.
/// </remarks>
public sealed class PostgresGrantStore : IGrantStore, IGrantQueryStore, IActivityGroupGrantStore
{
	private readonly IDbConnection _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresGrantStore"/> class.
	/// </summary>
	/// <param name="domainDb">The domain database connection provider.</param>
	public PostgresGrantStore(IDomainDb domainDb)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public async Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.GetGrant(userId, tenantId, grantType, qualifier, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.GetAllGrants(userId, cancellationToken);
		var result = await request.ResolveAsync(_connection).ConfigureAwait(false);
		return result.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.SaveGrant(grant, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.DeleteGrant(userId, tenantId, grantType, qualifier,
			revokedBy, revokedOn, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.GrantExists(userId, tenantId, grantType, qualifier, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IGrantQueryStore))
		{
			return this;
		}

		if (serviceType == typeof(IActivityGroupGrantStore))
		{
			return this;
		}

		return null;
	}

	// IGrantQueryStore

	/// <inheritdoc />
	public async Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string? userId, string tenantId,
		string grantType, string qualifier, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.GetMatchingGrants(userId, tenantId, grantType, qualifier, cancellationToken);
		var result = await request.ResolveAsync(_connection).ConfigureAwait(false);
		return result.ToList().AsReadOnly();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
		CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.FindUserGrants(userId, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	// IActivityGroupGrantStore

	/// <inheritdoc />
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
		CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.DeleteActivityGroupGrantsByUserId(userId, grantType, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> DeleteAllActivityGroupGrantsAsync(string grantType,
		CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.DeleteAllActivityGroupGrants(grantType, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<int> InsertActivityGroupGrantAsync(string userId, string fullName,
		string? tenantId, string grantType, string qualifier,
		DateTimeOffset? expiresOn, string grantedBy, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.InsertActivityGroupGrant(userId, fullName, tenantId,
			grantType, qualifier, expiresOn, grantedBy, cancellationToken);
		return await request.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(
		string grantType, CancellationToken cancellationToken)
	{
		var request = PostgresGrantRequestProvider.GetDistinctActivityGroupGrantUserIds(grantType, cancellationToken);
		var result = await request.ResolveAsync(_connection).ConfigureAwait(false);
		return result.ToList().AsReadOnly();
	}
}
