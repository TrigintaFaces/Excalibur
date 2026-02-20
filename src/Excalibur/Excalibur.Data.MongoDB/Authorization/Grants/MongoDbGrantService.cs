// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Authorization;

/// <summary>
/// MongoDB implementation of <see cref="IGrantRequestProvider"/>.
/// </summary>
/// <remarks>
/// Uses MongoDB.Driver with Filter.Builder for type-safe queries.
/// Implements upsert via ReplaceOneAsync with IsUpsert=true.
/// </remarks>
public sealed partial class MongoDbGrantService : IGrantRequestProvider, IGrantQueryProvider, IAsyncDisposable
{
	private readonly MongoDbAuthorizationOptions _options;
	private readonly ILogger<MongoDbGrantService> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<GrantDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbGrantService"/> class.
	/// </summary>
	/// <param name="options">The MongoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbGrantService(
		IOptions<MongoDbAuthorizationOptions> options,
		ILogger<MongoDbGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbGrantService"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbGrantService(
		IMongoClient client,
		IOptions<MongoDbAuthorizationOptions> options,
		ILogger<MongoDbGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<GrantDocument>(_options.GrantsCollectionName);
	}

	/// <inheritdoc/>
	public async Task<int> DeleteGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		string? revokedBy,
		DateTimeOffset? revokedOn,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<GrantDocument>.Filter.Eq(x => x.TenantId, tenantId),
			Builders<GrantDocument>.Filter.Eq(x => x.GrantType, grantType),
			Builders<GrantDocument>.Filter.Eq(x => x.Qualifier, qualifier));

		if (revokedBy is not null && revokedOn.HasValue)
		{
			// Soft delete by marking as revoked
			var update = Builders<GrantDocument>.Update
				.Set(x => x.IsRevoked, true)
				.Set(x => x.RevokedBy, revokedBy)
				.Set(x => x.RevokedOn, revokedOn);

			var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogGrantRevoked(userId, tenantId, grantType, qualifier);
			return (int)result.ModifiedCount;
		}
		else
		{
			// Hard delete
			var result = await _collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
			LogGrantDeleted(userId, tenantId, grantType, qualifier);
			return (int)result.DeletedCount;
		}
	}

	/// <inheritdoc/>
	public async Task<bool> GrantExistsAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<GrantDocument>.Filter.Eq(x => x.TenantId, tenantId),
			Builders<GrantDocument>.Filter.Eq(x => x.GrantType, grantType),
			Builders<GrantDocument>.Filter.Eq(x => x.Qualifier, qualifier),
			Builders<GrantDocument>.Filter.Eq(x => x.IsRevoked, false));

		var count = await _collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
		return count > 0;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(
		string? userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filters = new List<FilterDefinition<GrantDocument>>
		{
			Builders<GrantDocument>.Filter.Eq(x => x.TenantId, tenantId),
			Builders<GrantDocument>.Filter.Eq(x => x.GrantType, grantType),
			Builders<GrantDocument>.Filter.Eq(x => x.Qualifier, qualifier),
			Builders<GrantDocument>.Filter.Eq(x => x.IsRevoked, false)
		};

		if (userId is not null)
		{
			filters.Add(Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId));
		}

		var filter = Builders<GrantDocument>.Filter.And(filters);
		var documents = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);

		return documents.Select(d => d.ToGrant()).ToList();
	}

	/// <inheritdoc/>
	public async Task<Grant?> GetGrantAsync(
		string userId,
		string tenantId,
		string grantType,
		string qualifier,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<GrantDocument>.Filter.Eq(x => x.TenantId, tenantId),
			Builders<GrantDocument>.Filter.Eq(x => x.GrantType, grantType),
			Builders<GrantDocument>.Filter.Eq(x => x.Qualifier, qualifier),
			Builders<GrantDocument>.Filter.Eq(x => x.IsRevoked, false));

		var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return document?.ToGrant();
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<GrantDocument>.Filter.Eq(x => x.IsRevoked, false));

		var documents = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
		return documents.Select(d => d.ToGrant()).ToList();
	}

	/// <inheritdoc/>
	public async Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(grant);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, grant.UserId),
			Builders<GrantDocument>.Filter.Eq(x => x.TenantId, grant.TenantId),
			Builders<GrantDocument>.Filter.Eq(x => x.GrantType, grant.GrantType),
			Builders<GrantDocument>.Filter.Eq(x => x.Qualifier, grant.Qualifier));

		var document = GrantDocument.FromGrant(grant);
		var options = new ReplaceOptions { IsUpsert = true };

		var result = await _collection.ReplaceOneAsync(filter, document, options, cancellationToken).ConfigureAwait(false);
		LogGrantSaved(grant.UserId, grant.TenantId ?? "null", grant.GrantType, grant.Qualifier);

		return result.ModifiedCount > 0 || result.UpsertedId is not null ? 1 : 0;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<GrantDocument>.Filter.And(
			Builders<GrantDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<GrantDocument>.Filter.Eq(x => x.IsRevoked, false));

		var documents = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);

		// Return a dictionary keyed by grantType:qualifier
		var result = new Dictionary<string, object>();
		foreach (var doc in documents)
		{
			var key = $"{doc.GrantType}:{doc.Qualifier}";
			result[key] = doc.ToGrant();
		}

		return result;
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		if (serviceType == typeof(IGrantQueryProvider))
		{
			return this;
		}

		return null;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_client is IDisposable disposableClient)
		{
			disposableClient.Dispose();
		}

		await Task.CompletedTask.ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Ensures the MongoDB client, database, and collection are initialized.
	/// </summary>
	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client is null)
		{
			_client = new MongoClient(_options.ConnectionString);
		}

		_database ??= _client.GetDatabase(_options.DatabaseName);
		_collection ??= _database.GetCollection<GrantDocument>(_options.GrantsCollectionName);

		// Create indexes
		var indexKeys = Builders<GrantDocument>.IndexKeys
			.Ascending(x => x.UserId)
			.Ascending(x => x.TenantId)
			.Ascending(x => x.GrantType)
			.Ascending(x => x.Qualifier);

		var indexModel = new CreateIndexModel<GrantDocument>(
			indexKeys,
			new CreateIndexOptions { Unique = true, Name = "ix_grants_composite_unique" });

		_ = await _collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken).ConfigureAwait(false);

		// Additional indexes for common queries
		var userIdIndex = new CreateIndexModel<GrantDocument>(
			Builders<GrantDocument>.IndexKeys.Ascending(x => x.UserId),
			new CreateIndexOptions { Name = "ix_grants_userId" });

		var tenantTypeIndex = new CreateIndexModel<GrantDocument>(
			Builders<GrantDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GrantType),
			new CreateIndexOptions { Name = "ix_grants_tenantId_grantType" });

		_ = await _collection.Indexes.CreateManyAsync([userIdIndex, tenantTypeIndex], cancellationToken).ConfigureAwait(false);

		_initialized = true;
		LogInitialized(_options.DatabaseName, _options.GrantsCollectionName);
	}

	[LoggerMessage(DataMongoDbEventId.GrantServiceInitialized, LogLevel.Debug,
		"MongoDB grant service initialized for database '{DatabaseName}', collection '{CollectionName}'")]
	private partial void LogInitialized(string databaseName, string collectionName);

	[LoggerMessage(DataMongoDbEventId.GrantSaved, LogLevel.Debug,
		"Grant saved: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantSaved(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataMongoDbEventId.GrantDeleted, LogLevel.Debug,
		"Grant deleted: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantDeleted(string userId, string tenantId, string grantType, string qualifier);

	[LoggerMessage(DataMongoDbEventId.GrantRevoked, LogLevel.Debug,
		"Grant revoked: userId={UserId}, tenantId={TenantId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogGrantRevoked(string userId, string tenantId, string grantType, string qualifier);
}
