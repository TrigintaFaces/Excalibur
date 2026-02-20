// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Aggregation;

/// <summary>
/// Fluent builder for constructing MongoDB aggregation pipelines.
/// </summary>
/// <typeparam name="TDocument">The source document type.</typeparam>
/// <remarks>
/// <para>
/// Reference: MongoDB C# Driver <c>PipelineDefinitionBuilder</c> — fluent stage composition
/// with typed projection at the end.
/// </para>
/// <para>
/// Use the fluent methods (<see cref="Match"/>, <see cref="Group"/>, <see cref="Project"/>,
/// <see cref="Sort"/>, <see cref="Limit"/>) to compose the pipeline, then call
/// <see cref="Build"/> to create the executable pipeline.
/// </para>
/// </remarks>
public sealed class MongoAggregationBuilder<TDocument>
{
	private readonly IMongoCollection<TDocument> _collection;
	private readonly MongoAggregationOptions _options;
	private readonly List<BsonDocument> _stages = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoAggregationBuilder{TDocument}"/> class.
	/// </summary>
	/// <param name="collection">The MongoDB collection to aggregate.</param>
	/// <param name="options">The aggregation options.</param>
	public MongoAggregationBuilder(
		IMongoCollection<TDocument> collection,
		MongoAggregationOptions? options = null)
	{
		_collection = collection ?? throw new ArgumentNullException(nameof(collection));
		_options = options ?? new MongoAggregationOptions();
	}

	/// <summary>
	/// Adds a <c>$match</c> stage to filter documents.
	/// </summary>
	/// <param name="filter">The BSON filter document.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Match(BsonDocument filter)
	{
		ArgumentNullException.ThrowIfNull(filter);
		_stages.Add(new BsonDocument("$match", filter));
		return this;
	}

	/// <summary>
	/// Adds a <c>$group</c> stage to group documents.
	/// </summary>
	/// <param name="groupExpression">The BSON group expression document.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Group(BsonDocument groupExpression)
	{
		ArgumentNullException.ThrowIfNull(groupExpression);
		_stages.Add(new BsonDocument("$group", groupExpression));
		return this;
	}

	/// <summary>
	/// Adds a <c>$project</c> stage to reshape documents.
	/// </summary>
	/// <param name="projection">The BSON projection document.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Project(BsonDocument projection)
	{
		ArgumentNullException.ThrowIfNull(projection);
		_stages.Add(new BsonDocument("$project", projection));
		return this;
	}

	/// <summary>
	/// Adds a <c>$sort</c> stage to order documents.
	/// </summary>
	/// <param name="sortExpression">The BSON sort expression document.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Sort(BsonDocument sortExpression)
	{
		ArgumentNullException.ThrowIfNull(sortExpression);
		_stages.Add(new BsonDocument("$sort", sortExpression));
		return this;
	}

	/// <summary>
	/// Adds a <c>$limit</c> stage to restrict the number of output documents.
	/// </summary>
	/// <param name="limit">The maximum number of documents.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Limit(int limit)
	{
		_stages.Add(new BsonDocument("$limit", limit));
		return this;
	}

	/// <summary>
	/// Adds a <c>$skip</c> stage to skip documents.
	/// </summary>
	/// <param name="count">The number of documents to skip.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Skip(int count)
	{
		_stages.Add(new BsonDocument("$skip", count));
		return this;
	}

	/// <summary>
	/// Adds a <c>$unwind</c> stage to deconstruct an array field.
	/// </summary>
	/// <param name="fieldPath">The field path to unwind (e.g., "$items").</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> Unwind(string fieldPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);
		_stages.Add(new BsonDocument("$unwind", fieldPath));
		return this;
	}

	/// <summary>
	/// Adds a raw BSON stage to the pipeline.
	/// </summary>
	/// <param name="stage">The BSON document representing the stage.</param>
	/// <returns>The builder for method chaining.</returns>
	public MongoAggregationBuilder<TDocument> AddRawStage(BsonDocument stage)
	{
		ArgumentNullException.ThrowIfNull(stage);
		_stages.Add(stage);
		return this;
	}

	/// <summary>
	/// Builds the aggregation pipeline and returns the executable pipeline instance.
	/// </summary>
	/// <returns>The executable <see cref="IMongoAggregationPipeline{TDocument}"/>.</returns>
	public IMongoAggregationPipeline<TDocument> Build()
	{
		return new MongoAggregationPipeline<TDocument>(_collection, [.. _stages], _options);
	}
}
