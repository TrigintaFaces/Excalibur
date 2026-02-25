// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Aggregation;

/// <summary>
/// Executes a MongoDB aggregation pipeline against a collection.
/// </summary>
/// <typeparam name="TDocument">The source document type.</typeparam>
internal sealed class MongoAggregationPipeline<TDocument> : IMongoAggregationPipeline<TDocument>
{
	private readonly IMongoCollection<TDocument> _collection;
	private readonly List<BsonDocument> _stages;
	private readonly MongoAggregationOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoAggregationPipeline{TDocument}"/> class.
	/// </summary>
	/// <param name="collection">The MongoDB collection.</param>
	/// <param name="stages">The pipeline stages.</param>
	/// <param name="options">The aggregation options.</param>
	internal MongoAggregationPipeline(
		IMongoCollection<TDocument> collection,
		List<BsonDocument> stages,
		MongoAggregationOptions options)
	{
		_collection = collection ?? throw new ArgumentNullException(nameof(collection));
		_stages = stages ?? throw new ArgumentNullException(nameof(stages));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public IMongoAggregationPipeline<TDocument> AddStage(BsonDocument stage)
	{
		ArgumentNullException.ThrowIfNull(stage);
		_stages.Add(stage);
		return this;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TResult>> ExecuteAsync<TResult>(CancellationToken cancellationToken)
	{
		var pipelineDefinition = PipelineDefinition<TDocument, BsonDocument>.Create(_stages);

		var aggregateOptions = new AggregateOptions
		{
			AllowDiskUse = _options.AllowDiskUse,
			MaxTime = _options.MaxTime,
			BatchSize = _options.BatchSize
		};

		if (!string.IsNullOrWhiteSpace(_options.Collation))
		{
			aggregateOptions.Collation = new Collation(_options.Collation);
		}

		using var cursor = await _collection.AggregateAsync(pipelineDefinition, aggregateOptions, cancellationToken)
			.ConfigureAwait(false);

		var results = new List<TResult>();
		while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
		{
			foreach (var doc in cursor.Current)
			{
				var result = BsonSerializer.Deserialize<TResult>(doc);
				results.Add(result);
			}
		}

		return results;
	}

	/// <inheritdoc />
	public IReadOnlyList<BsonDocument> GetStages() => _stages.AsReadOnly();
}
