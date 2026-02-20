// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;

namespace Excalibur.Data.MongoDB.Aggregation;

/// <summary>
/// Defines a composable MongoDB aggregation pipeline for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The source document type.</typeparam>
/// <remarks>
/// Reference: MongoDB C# Driver <c>IAggregateFluent</c> pattern -- provides a fluent API
/// for building aggregation pipelines with stage-by-stage composition.
/// </remarks>
public interface IMongoAggregationPipeline<TDocument>
{
	/// <summary>
	/// Adds a raw BSON aggregation stage to the pipeline.
	/// </summary>
	/// <param name="stage">The BSON document representing the aggregation stage.</param>
	/// <returns>The pipeline instance for method chaining.</returns>
	IMongoAggregationPipeline<TDocument> AddStage(BsonDocument stage);

	/// <summary>
	/// Executes the aggregation pipeline and returns the results.
	/// </summary>
	/// <typeparam name="TResult">The type of the result documents.</typeparam>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The aggregation results.</returns>
	Task<IReadOnlyList<TResult>> ExecuteAsync<TResult>(CancellationToken cancellationToken);

	/// <summary>
	/// Gets the list of pipeline stages as BSON documents for inspection.
	/// </summary>
	/// <returns>The pipeline stages.</returns>
	IReadOnlyList<BsonDocument> GetStages();
}
