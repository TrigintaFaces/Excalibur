using System.Data;
using System.Diagnostics;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.QueryProviders;
using Excalibur.Data;
using Excalibur.Data.Outbox;
using Excalibur.Data.Repositories;
using Excalibur.DataAccess.Exceptions;
using Excalibur.Domain;
using Excalibur.Domain.Exceptions;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Authorization.Grants.Domain.Repositories;

/// <summary>
///     Represents a repository for managing <see cref="Grant" /> entities, providing CRUD operations and utility methods for handling
///     grants in a relational database.
/// </summary>
public class GrantRepository : AggregateRepository<Grant, string>, IGrantRepository
{
	private readonly IGrantQueryProvider _queryProvider;
	private readonly IDbConnection _connection;
	private readonly ILogger<GrantRepository> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="GrantRepository" /> class.
	/// </summary>
	/// <param name="queryProvider"> The database specific query provider. </param>
	/// <param name="context"> The activity context containing metadata about the current activity. </param>
	/// <param name="outbox"> The outbox used to queue messages for eventual consistency. </param>
	/// <param name="logger"> Logger for logging messages and errors. </param>
	public GrantRepository(IGrantQueryProvider queryProvider, IActivityContext context, IOutbox outbox, ILogger<GrantRepository> logger) :
		base(context, outbox)
	{
		_queryProvider = queryProvider;
		_logger = logger;
		_connection = context.DomainDb().Connection;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> Matching(GrantScope scope, string? userId = null)
	{
		ArgumentNullException.ThrowIfNull(scope);
		ArgumentException.ThrowIfNullOrEmpty(userId);

		var query = _queryProvider.GetMatchingGrants(userId, scope.TenantId, scope.GrantType, scope.Qualifier, CancellationToken.None);
		return await query.Resolve(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<Grant>> ReadAll(string userId)
	{
		ArgumentException.ThrowIfNullOrEmpty(userId);

		try
		{
			var query = _queryProvider.GetAllGrants(userId, CancellationToken.None);
			return await query.Resolve(_connection).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			throw new OperationFailedException(
				TypeNameHelper.GetTypeDisplayName(typeof(Grant), false),
				nameof(ReadAll),
				innerException: ex);
		}
	}

	/// <inheritdoc />
	protected override async Task<int> DeleteInternal(Grant aggregate, string eTag, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		DomainException.ThrowIf(!aggregate.IsRevoked() || aggregate.IsExpired(), statusCode: 400,
			message: "A grant must be revoked or expired before deleting it.");

		var query = _queryProvider.DeleteGrant(
			aggregate.UserId, aggregate.Scope.TenantId, aggregate.Scope.GrantType, aggregate.Scope.Qualifier, aggregate.RevokedBy,
			aggregate.RevokedOn, cancellationToken);

		return await query.Resolve(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task<bool> ExistsInternal(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);
		var id = new GrantKey(key);

		var query = _queryProvider.GrantExists(id.UserId, id.Scope.TenantId, id.Scope.GrantType, id.Scope.Qualifier,
			CancellationToken.None);

		return await query.Resolve(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task<Grant?> ReadInternal(string key, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(key);

		var id = new GrantKey(key);

		var query = _queryProvider.GetGrant(id.UserId, id.Scope.TenantId, id.Scope.GrantType, id.Scope.Qualifier, CancellationToken.None);

		return await query.Resolve(_connection).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task<int> SaveInternal(Grant aggregate, string eTag, string newETag, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		DomainException.ThrowIf(!aggregate.IsActive(), statusCode: 400, message: "Only active grants can be saved.");

		try
		{
			var query = _queryProvider.SaveGrant(aggregate, CancellationToken.None);
			return await query.Resolve(_connection).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex,
				"Error saving grant with UserId: {UserId}, TenantId: {TenantId}, GrantType: {GrantType}, Qualifier: {Qualifier}.",
				aggregate.UserId, aggregate.Scope.TenantId, aggregate.Scope.GrantType, aggregate.Scope.Qualifier);
			throw;
		}
	}
}
