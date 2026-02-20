// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics;

using Excalibur.A3.Diagnostics;
using Excalibur.Data;
using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Domain.Exceptions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a repository for managing <see cref="Grant" /> entities, providing CRUD operations and utility methods for handling grants in
/// a relational database.
/// </summary>
/// <remarks>
/// <para>
/// This repository implements the <see cref="IEventSourcedRepository{TAggregate}"/> pattern from Excalibur.EventSourcing
/// while storing Grant data in a traditional relational schema (not an event store).
/// </para>
/// <para>
/// The Grant aggregate uses event sourcing for in-memory state management but persists
/// the current state snapshot to SQL Server tables for efficient querying.
/// </para>
/// </remarks>
public partial class GrantRepository : IGrantRepository
{
	private readonly IGrantRequestProvider _requestProvider;
	private readonly IDbConnection _connection;
	private readonly ILogger<GrantRepository> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrantRepository" /> class.
	/// </summary>
	/// <param name="domainDb"> The domain database connection provider. </param>
	/// <param name="requestProvider"> The database specific request provider. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public GrantRepository(
		IDomainDb domainDb,
		IGrantRequestProvider requestProvider,
		ILogger<GrantRepository> logger)
	{
		ArgumentNullException.ThrowIfNull(domainDb);
		ArgumentNullException.ThrowIfNull(requestProvider);
		ArgumentNullException.ThrowIfNull(logger);

		_requestProvider = requestProvider;
		_logger = logger;
		_connection = domainDb.Connection;
	}

	/// <inheritdoc />
	public async Task<Grant?> GetByIdAsync(string aggregateId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);

		var id = new GrantKey(aggregateId);
		var getGrant = _requestProvider.GetGrant(
			id.UserId,
			id.Scope.TenantId,
			id.Scope.GrantType,
			id.Scope.Qualifier,
			cancellationToken);

		return await getGrant.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task SaveAsync(Grant aggregate, CancellationToken cancellationToken) =>
		SaveAsync(aggregate, expectedETag: null, cancellationToken);

	/// <inheritdoc />
	public async Task SaveAsync(Grant aggregate, string? expectedETag, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		DomainException.ThrowIf(!aggregate.IsActive(), "Only active grants can be saved.");

		// ETag validation if expected
		if (expectedETag is not null && aggregate.ETag != expectedETag)
		{
			throw new ConcurrencyException(
				aggregate.AggregateType,
				aggregate.Id,
				expectedETag,
				aggregate.ETag ?? string.Empty);
		}

		try
		{
			if (aggregate.UserId is null || aggregate.Scope is null)
			{
				throw new InvalidOperationException("Grant UserId and Scope cannot be null.");
			}

			var saveGrant = _requestProvider.SaveGrant(aggregate, cancellationToken);
			_ = await saveGrant.ResolveAsync(_connection).ConfigureAwait(false);

			// Mark events as committed after successful save
			aggregate.MarkEventsAsCommitted();
		}
		catch (Exception ex) when (ex is not ConcurrencyException)
		{
			if (_logger.IsEnabled(LogLevel.Error))
			{
				LogGrantSaveError(
					ex,
					aggregate.UserId,
					aggregate.Scope?.TenantId,
					aggregate.Scope?.GrantType,
					aggregate.Scope?.Qualifier);
			}

			throw;
		}
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(string aggregateId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);
		var id = new GrantKey(aggregateId);

		var grantExists = _requestProvider.GrantExists(
			id.UserId,
			id.Scope.TenantId,
			id.Scope.GrantType,
			id.Scope.Qualifier,
			cancellationToken);

		return await grantExists.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DeleteAsync(Grant aggregate, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		DomainException.ThrowIf(
			!aggregate.IsRevoked() && !aggregate.IsExpired(),
			"A grant must be revoked or expired before deleting it.");

		if (aggregate.UserId is null)
		{
			throw new InvalidOperationException("Grant UserId cannot be null.");
		}

		if (aggregate.Scope is null)
		{
			throw new InvalidOperationException("Grant Scope cannot be null.");
		}

		var deleteGrant = _requestProvider.DeleteGrant(
			aggregate.UserId,
			aggregate.Scope.TenantId,
			aggregate.Scope.GrantType,
			aggregate.Scope.Qualifier,
			aggregate.RevokedBy,
			aggregate.RevokedOn,
			cancellationToken);

		_ = await deleteGrant.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<Grant>> QueryAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<Grant>
	{
		ArgumentNullException.ThrowIfNull(query);

		// For now, return empty collection as GrantQuery doesn't have specific criteria
		// This would typically be implemented based on query properties
		return await Task.FromResult<IReadOnlyList<Grant>>([]).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<Grant?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
		where TQuery : IAggregateQuery<Grant>
	{
		ArgumentNullException.ThrowIfNull(query);

		// For now, return null as GrantQuery doesn't have specific criteria
		// This would typically be implemented based on query properties
		return await Task.FromResult<Grant?>(null).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> MatchingAsync(GrantScope scope, string? userId = null)
	{
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentException.ThrowIfNullOrEmpty(userId);

		var getMatchingGrants = _requestProvider.GetMatchingGrants(
			userId,
			scope.TenantId,
			scope.GrantType,
			scope.Qualifier,
			CancellationToken.None);

		return await getMatchingGrants.ResolveAsync(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> ReadAllAsync(string userId)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);

		try
		{
			var getAllGrants = _requestProvider.GetAllGrants(userId, CancellationToken.None);
			return await getAllGrants.ResolveAsync(_connection).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(
				TypeNameHelper.GetTypeDisplayName(typeof(Grant), fullName: false),
				nameof(ReadAllAsync),
				innerException: ex);
		}
	}

	[LoggerMessage(A3EventId.GrantSaveError, LogLevel.Error, "Error saving grant with UserId: {UserId}, TenantId: {TenantId}, GrantType: {GrantType}, Qualifier: {Qualifier}.")]
	private partial void LogGrantSaveError(Exception ex, string? userId, string? tenantId, string? grantType, string? qualifier);
}
