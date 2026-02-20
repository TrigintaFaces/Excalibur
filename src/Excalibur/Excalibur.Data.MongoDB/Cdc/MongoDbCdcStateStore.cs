// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// MongoDB implementation of <see cref="IMongoDbCdcStateStore"/> using a state collection.
/// </summary>
public sealed class MongoDbCdcStateStore : IMongoDbCdcStateStore
{
	private readonly IMongoCollection<CdcStateDocument> _collection;
	private readonly Lazy<Task> _indexCreation;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcStateStore"/> class with an existing client and options.
	/// </summary>
	/// <param name="client">The MongoDB client.</param>
	/// <param name="options">The CDC state store options.</param>
	public MongoDbCdcStateStore(
		IMongoClient client,
		IOptions<MongoDbCdcStateStoreOptions> options)
		: this(client, options?.Value ?? throw new ArgumentNullException(nameof(options)))
	{
	}

	private MongoDbCdcStateStore(IMongoClient client, MongoDbCdcStateStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);

		options.Validate();

		var database = client.GetDatabase(options.DatabaseName);
		_collection = database.GetCollection<CdcStateDocument>(options.CollectionName);

		// Defer index creation to first async operation to avoid sync-over-async blocking
		_indexCreation = new Lazy<Task>(() => CreateIndexesAsync());
	}

	/// <summary>
	/// Ensures indexes are created before first data operation.
	/// </summary>
	private async Task EnsureIndexesAsync()
	{
		await _indexCreation.Value.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<MongoDbCdcPosition> GetLastPositionAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.And(
			Builders<CdcStateDocument>.Filter.Eq(x => x.ProcessorId, processorId),
			Builders<CdcStateDocument>.Filter.Eq(x => x.Namespace, null));

		var document = await _collection
			.Find(filter)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return MongoDbCdcPosition.FromString(document?.ResumeToken);
	}

	/// <inheritdoc/>
	public async Task SavePositionAsync(
		string processorId,
		MongoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.And(
			Builders<CdcStateDocument>.Filter.Eq(x => x.ProcessorId, processorId),
			Builders<CdcStateDocument>.Filter.Eq(x => x.Namespace, null));

		var update = Builders<CdcStateDocument>.Update
			.Set(x => x.ResumeToken, position.TokenString)
			.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
			.SetOnInsert(x => x.ProcessorId, processorId)
			.SetOnInsert(x => x.Namespace, null);

		_ = await _collection
			.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<MongoDbCdcStateEntry>> GetAllStatesAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.Eq(x => x.ProcessorId, processorId);

		var documents = await _collection
			.Find(filter)
			.SortBy(x => x.Namespace)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => new MongoDbCdcStateEntry
		{
			ProcessorId = d.ProcessorId,
			Namespace = d.Namespace,
			ResumeToken = d.ResumeToken ?? string.Empty,
			LastEventTime = d.LastEventTime,
			UpdatedAt = d.UpdatedAt,
			EventCount = d.EventCount,
		}).ToList();
	}

	/// <inheritdoc/>
	public async Task SaveStateAsync(
		MongoDbCdcStateEntry entry,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(entry);
		ArgumentException.ThrowIfNullOrWhiteSpace(entry.ProcessorId);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.And(
			Builders<CdcStateDocument>.Filter.Eq(x => x.ProcessorId, entry.ProcessorId),
			Builders<CdcStateDocument>.Filter.Eq(x => x.Namespace, entry.Namespace));

		var update = Builders<CdcStateDocument>.Update
			.Set(x => x.ResumeToken, entry.ResumeToken)
			.Set(x => x.LastEventTime, entry.LastEventTime)
			.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
			.Inc(x => x.EventCount, entry.EventCount)
			.SetOnInsert(x => x.ProcessorId, entry.ProcessorId)
			.SetOnInsert(x => x.Namespace, entry.Namespace);

		_ = await _collection
			.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task ClearStateAsync(
		string processorId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.Eq(x => x.ProcessorId, processorId);

		_ = await _collection
			.DeleteManyAsync(filter, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		var position = await GetLastPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return position.IsValid ? position.ToChangePosition() : null;
	}

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		var mongoPosition = MongoDbCdcPosition.FromChangePosition(position);
		return SavePositionAsync(consumerId, mongoPosition, cancellationToken);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await ClearStateAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureIndexesAsync().ConfigureAwait(false);

		var filter = Builders<CdcStateDocument>.Filter.Eq(x => x.Namespace, null);
		var documents = await _collection.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);

		foreach (var doc in documents)
		{
			var position = MongoDbCdcPosition.FromString(doc.ResumeToken);
			if (position.IsValid)
			{
				yield return (doc.ProcessorId, position.ToChangePosition());
			}
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}

	private async Task CreateIndexesAsync()
	{
		var indexKeys = Builders<CdcStateDocument>.IndexKeys
			.Ascending(x => x.ProcessorId)
			.Ascending(x => x.Namespace);

		var indexOptions = new CreateIndexOptions { Unique = true };

		_ = await _collection.Indexes
			.CreateOneAsync(new CreateIndexModel<CdcStateDocument>(indexKeys, indexOptions))
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Internal document model for CDC state storage.
	/// </summary>
	[BsonIgnoreExtraElements]
	private sealed class CdcStateDocument
	{
		[BsonId]
		[BsonRepresentation(BsonType.ObjectId)]
		public string? Id { get; set; }

		[BsonElement("processor_id")] public string ProcessorId { get; set; } = string.Empty;

		[BsonElement("namespace")] public string? Namespace { get; set; }

		[BsonElement("resume_token")] public string? ResumeToken { get; set; }

		[BsonElement("last_event_time")] public DateTimeOffset? LastEventTime { get; set; }

		[BsonElement("updated_at")] public DateTimeOffset UpdatedAt { get; set; }

		[BsonElement("event_count")] public long EventCount { get; set; }
	}
}
