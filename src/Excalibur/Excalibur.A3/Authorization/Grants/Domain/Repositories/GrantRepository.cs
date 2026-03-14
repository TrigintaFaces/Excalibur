// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Diagnostics;
using Excalibur.Data.Abstractions;
using Excalibur.Domain.Exceptions;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;

using StoreGrant = Excalibur.A3.Abstractions.Authorization.Grant;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Represents a repository for managing <see cref="Grant" /> entities, providing CRUD operations and utility methods for handling grants.
/// </summary>
/// <remarks>
/// <para>
/// This repository implements the <see cref="IEventSourcedRepository{TAggregate}"/> pattern from Excalibur.EventSourcing
/// while delegating persistence to an <see cref="IGrantStore"/> implementation.
/// </para>
/// <para>
/// The Grant aggregate uses event sourcing for in-memory state management but persists
/// the current state snapshot via the provider-neutral store interface.
/// </para>
/// </remarks>
public partial class GrantRepository : IGrantRepository
{
	private readonly IGrantStore _grantStore;
	private readonly IGrantQueryStore? _queryStore;
	private readonly ILogger<GrantRepository> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="GrantRepository" /> class.
	/// </summary>
	/// <param name="grantStore"> The provider-neutral grant store. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public GrantRepository(
		IGrantStore grantStore,
		ILogger<GrantRepository> logger)
	{
		ArgumentNullException.ThrowIfNull(grantStore);
		ArgumentNullException.ThrowIfNull(logger);

		_grantStore = grantStore;
		_queryStore = grantStore.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<Grant?> GetByIdAsync(string aggregateId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(aggregateId);

		var id = new GrantKey(aggregateId);
		var storeGrant = await _grantStore.GetGrantAsync(
			id.UserId,
			id.Scope.TenantId,
			id.Scope.GrantType,
			id.Scope.Qualifier,
			cancellationToken).ConfigureAwait(false);

		return storeGrant is null ? null : ToDomainGrant(storeGrant);
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

			var storeGrant = ToStoreGrant(aggregate);
			_ = await _grantStore.SaveGrantAsync(storeGrant, cancellationToken).ConfigureAwait(false);

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

		return await _grantStore.GrantExistsAsync(
			id.UserId,
			id.Scope.TenantId,
			id.Scope.GrantType,
			id.Scope.Qualifier,
			cancellationToken).ConfigureAwait(false);
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

		_ = await _grantStore.DeleteGrantAsync(
			aggregate.UserId,
			aggregate.Scope.TenantId,
			aggregate.Scope.GrantType,
			aggregate.Scope.Qualifier,
			aggregate.RevokedBy,
			aggregate.RevokedOn,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> MatchingAsync(GrantScope scope, string? userId = null)
	{
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentException.ThrowIfNullOrEmpty(userId);

		if (_queryStore is null)
		{
			throw new InvalidOperationException(
				"The configured IGrantStore does not support IGrantQueryStore. " +
				"Ensure the store implementation returns IGrantQueryStore from GetService().");
		}

		var storeGrants = await _queryStore.GetMatchingGrantsAsync(
			userId,
			scope.TenantId,
			scope.GrantType,
			scope.Qualifier,
			CancellationToken.None).ConfigureAwait(false);

		return storeGrants.Select(ToDomainGrant);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> ReadAllAsync(string userId)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);

		try
		{
			var storeGrants = await _grantStore.GetAllGrantsAsync(userId, CancellationToken.None).ConfigureAwait(false);
			return storeGrants.Select(ToDomainGrant);
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(
				nameof(Grant),
				nameof(ReadAllAsync),
				innerException: ex);
		}
	}

	/// <summary>
	/// Maps a store DTO grant to a domain aggregate grant.
	/// </summary>
	private static Grant ToDomainGrant(StoreGrant storeGrant)
	{
		var grant = Grant.Create(
			$"{storeGrant.UserId}:{storeGrant.TenantId}:{storeGrant.GrantType}:{storeGrant.Qualifier}");

		grant.UserId = storeGrant.UserId;
		grant.FullName = storeGrant.FullName;
		grant.Scope = new GrantScope(
			storeGrant.TenantId ?? string.Empty,
			storeGrant.GrantType,
			storeGrant.Qualifier);
		grant.ExpiresOn = storeGrant.ExpiresOn;
		grant.GrantedBy = storeGrant.GrantedBy;
		grant.GrantedOn = storeGrant.GrantedOn;

		return grant;
	}

	/// <summary>
	/// Maps a domain aggregate grant to a store DTO grant.
	/// </summary>
	private static StoreGrant ToStoreGrant(Grant aggregate) =>
		new(
			UserId: aggregate.UserId ?? string.Empty,
			FullName: aggregate.FullName,
			TenantId: aggregate.Scope?.TenantId,
			GrantType: aggregate.Scope?.GrantType ?? string.Empty,
			Qualifier: aggregate.Scope?.Qualifier ?? string.Empty,
			ExpiresOn: aggregate.ExpiresOn,
			GrantedBy: aggregate.GrantedBy ?? string.Empty,
			GrantedOn: aggregate.GrantedOn);

	[LoggerMessage(A3EventId.GrantSaveError, LogLevel.Error, "Error saving grant with UserId: {UserId}, TenantId: {TenantId}, GrantType: {GrantType}, Qualifier: {Qualifier}.")]
	private partial void LogGrantSaveError(Exception ex, string? userId, string? tenantId, string? grantType, string? qualifier);
}
