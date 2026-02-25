// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch.IndexManagement;


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Provides functionality for dynamic index operations including creation, optimization, and health monitoring.
/// </summary>
public interface IIndexOperationsManager
{
	/// <summary>
	/// Creates an index with the specified configuration.
	/// </summary>
	/// <param name="indexName"> The name of the index to create. </param>
	/// <param name="configuration"> The index configuration settings. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> CreateIndexAsync(string indexName, IndexConfiguration configuration, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes an index from Elasticsearch.
	/// </summary>
	/// <param name="indexName"> The name of the index to delete. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken);

	/// <summary>
	/// Checks if an index exists in Elasticsearch.
	/// </summary>
	/// <param name="indexName"> The name of the index to check. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the index exists. </returns>
	Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken);

	/// <summary>
	/// Gets health information for the specified indices.
	/// </summary>
	/// <param name="indexPattern"> The index pattern to check. If null, checks all indices. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IEnumerable}" /> containing index health information. </returns>
	Task<IEnumerable<IndexHealthStatus>> GetIndexHealthAsync(string? indexPattern, CancellationToken cancellationToken);

	/// <summary>
	/// Optimizes index settings based on usage patterns.
	/// </summary>
	/// <param name="indexName"> The name of the index to optimize. </param>
	/// <param name="optimizationOptions"> The optimization options. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{IndexOptimizationResult}" /> containing optimization results. </returns>
	Task<IndexOptimizationResult> OptimizeIndexAsync(string indexName, IndexOptimizationOptions optimizationOptions,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates index settings dynamically.
	/// </summary>
	/// <param name="indexName"> The name of the index to update. </param>
	/// <param name="settings"> The new settings to apply. </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> UpdateIndexSettingsAsync(string indexName, IndexSettings settings, CancellationToken cancellationToken);

	/// <summary>
	/// Forces a merge operation on the specified index.
	/// </summary>
	/// <param name="indexName"> The name of the index to merge. </param>
	/// <param name="maxNumSegments">
	/// The maximum number of segments to merge Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. If null, uses Elasticsearch default.
	/// </param>
	/// <param name="cancellationToken"> The cancellation token to cancel the operation if required. </param>
	/// <returns> A <see cref="Task{Boolean}" /> indicating whether the operation was successful. </returns>
	Task<bool> ForceMergeAsync(string indexName, int? maxNumSegments, CancellationToken cancellationToken);
}
