// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.Data.MongoDB.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Authorization;

/// <summary>
/// MongoDB implementation of <see cref="IActivityGroupGrantService"/>.
/// </summary>
/// <remarks>
/// Uses MongoDB.Driver with Filter.Builder for type-safe queries.
/// Implements upsert via ReplaceOneAsync with IsUpsert=true.
/// </remarks>
public sealed partial class MongoDbActivityGroupGrantService : IActivityGroupGrantService, IAsyncDisposable
{
	private readonly MongoDbAuthorizationOptions _options;
	private readonly ILogger<MongoDbActivityGroupGrantService> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<ActivityGroupDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbActivityGroupGrantService"/> class.
	/// </summary>
	/// <param name="options">The MongoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbActivityGroupGrantService(
		IOptions<MongoDbAuthorizationOptions> options,
		ILogger<MongoDbActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbActivityGroupGrantService"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB authorization options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbActivityGroupGrantService(
		IMongoClient client,
		IOptions<MongoDbAuthorizationOptions> options,
		ILogger<MongoDbActivityGroupGrantService> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<ActivityGroupDocument>(_options.ActivityGroupsCollectionName);
	}

	/// <inheritdoc/>
	public async Task<int> DeleteActivityGroupGrantsByUserIdAsync(string userId, string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<ActivityGroupDocument>.Filter.And(
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.GrantType, grantType));

		var result = await _collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
		LogActivityGroupGrantsDeletedByUser(userId, grantType, (int)result.DeletedCount);

		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> DeleteAllActivityGroupGrantsAsync(string grantType, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<ActivityGroupDocument>.Filter.Eq(x => x.GrantType, grantType);
		var result = await _collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
		LogAllActivityGroupGrantsDeleted(grantType, (int)result.DeletedCount);

		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	public async Task<int> InsertActivityGroupGrantAsync(
		string userId,
		string fullName,
		string? tenantId,
		string grantType,
		string qualifier,
		DateTimeOffset? expiresOn,
		string grantedBy,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var document = new ActivityGroupDocument
		{
			Id = ActivityGroupDocument.CreateId(userId, tenantId, grantType, qualifier),
			UserId = userId,
			FullName = fullName,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier,
			ExpiresOn = expiresOn,
			GrantedBy = grantedBy,
			CreatedAt = now,
			UpdatedAt = now
		};

		// Use upsert to handle duplicates gracefully
		var filter = Builders<ActivityGroupDocument>.Filter.And(
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.UserId, userId),
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.TenantId, tenantId),
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.GrantType, grantType),
			Builders<ActivityGroupDocument>.Filter.Eq(x => x.Qualifier, qualifier));

		var options = new ReplaceOptions { IsUpsert = true };
		var result = await _collection.ReplaceOneAsync(filter, document, options, cancellationToken).ConfigureAwait(false);

		LogActivityGroupGrantInserted(userId, grantType, qualifier);
		return result.ModifiedCount > 0 || result.UpsertedId is not null ? 1 : 0;
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetDistinctActivityGroupGrantUserIdsAsync(string grantType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<ActivityGroupDocument>.Filter.Eq(x => x.GrantType, grantType);
		var userIds = await _collection.DistinctAsync(
			new StringFieldDefinition<ActivityGroupDocument, string>("userId"),
			filter,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return await userIds.ToListAsync(cancellationToken).ConfigureAwait(false);
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
		_collection ??= _database.GetCollection<ActivityGroupDocument>(_options.ActivityGroupsCollectionName);

		// Create indexes
		var compositeIndex = new CreateIndexModel<ActivityGroupDocument>(
			Builders<ActivityGroupDocument>.IndexKeys
				.Ascending(x => x.UserId)
				.Ascending(x => x.TenantId)
				.Ascending(x => x.GrantType)
				.Ascending(x => x.Qualifier),
			new CreateIndexOptions { Unique = true, Name = "ix_activity_groups_composite_unique" });

		var userIdIndex = new CreateIndexModel<ActivityGroupDocument>(
			Builders<ActivityGroupDocument>.IndexKeys.Ascending(x => x.UserId),
			new CreateIndexOptions { Name = "ix_activity_groups_userId" });

		var grantTypeIndex = new CreateIndexModel<ActivityGroupDocument>(
			Builders<ActivityGroupDocument>.IndexKeys.Ascending(x => x.GrantType),
			new CreateIndexOptions { Name = "ix_activity_groups_grantType" });

		_ = await _collection.Indexes.CreateManyAsync([compositeIndex, userIdIndex, grantTypeIndex], cancellationToken).ConfigureAwait(false);

		_initialized = true;
		LogInitialized(_options.DatabaseName, _options.ActivityGroupsCollectionName);
	}

	[LoggerMessage(DataMongoDbEventId.ActivityGroupServiceInitialized, LogLevel.Debug,
		"MongoDB activity group service initialized for database '{DatabaseName}', collection '{CollectionName}'")]
	private partial void LogInitialized(string databaseName, string collectionName);

	[LoggerMessage(DataMongoDbEventId.ActivityGroupGrantSaved, LogLevel.Debug,
		"Activity group grant inserted: userId={UserId}, grantType={GrantType}, qualifier={Qualifier}")]
	private partial void LogActivityGroupGrantInserted(string userId, string grantType, string qualifier);

	[LoggerMessage(DataMongoDbEventId.ActivityGroupGrantsDeletedByUser, LogLevel.Debug,
		"Activity group grants deleted by user: userId={UserId}, grantType={GrantType}, count={Count}")]
	private partial void LogActivityGroupGrantsDeletedByUser(string userId, string grantType, int count);

	[LoggerMessage(DataMongoDbEventId.ActivityGroupAllGrantsDeleted, LogLevel.Debug,
		"All activity group grants deleted: grantType={GrantType}, count={Count}")]
	private partial void LogAllActivityGroupGrantsDeleted(string grantType, int count);
}
