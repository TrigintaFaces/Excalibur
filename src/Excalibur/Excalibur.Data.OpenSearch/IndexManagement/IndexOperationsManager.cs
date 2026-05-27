// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using OpenSearch.Client;
using OpenSearch.Net;

using HttpMethod = OpenSearch.Net.HttpMethod;

namespace Excalibur.Data.OpenSearch.IndexManagement;

/// <summary>
/// Provides functionality for dynamic index operations including creation, optimization, and health monitoring.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="IndexOperationsManager" /> class.
/// </remarks>
/// <param name="client"> The OpenSearch client instance. </param>
/// <param name="logger"> The logger instance. </param>
/// <exception cref="ArgumentNullException"> Thrown if any parameter is null. </exception>
internal sealed class IndexOperationsManager(IOpenSearchClient client, ILogger<IndexOperationsManager> logger) : IIndexOperationsManager
{
	private readonly IOpenSearchClient _client = client ?? throw new ArgumentNullException(nameof(client));
	private readonly ILogger<IndexOperationsManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "OpenSearch low-level API requires runtime serialization of opaque JSON payloads.")]
	public async Task<bool> CreateIndexAsync(string indexName, IndexConfiguration configuration,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentNullException.ThrowIfNull(configuration);

		try
		{
			_logger.LogInformation("Creating index: {IndexName}", indexName);

			// Build index creation body from opaque JSON payloads
			var body = new Dictionary<string, object?>(StringComparer.Ordinal);
			if (configuration.SettingsJson.HasValue)
			{
				body["settings"] = configuration.SettingsJson.Value;
			}

			if (configuration.MappingsJson.HasValue)
			{
				body["mappings"] = configuration.MappingsJson.Value;
			}

			if (configuration.AliasesJson.HasValue)
			{
				body["aliases"] = configuration.AliasesJson.Value;
			}

			var response = await _client.LowLevel.DoRequestAsync<StringResponse>(
				HttpMethod.PUT,
				$"{Uri.EscapeDataString(indexName)}",
				cancellationToken,
				body.Count > 0
					? PostData.String(JsonSerializer.Serialize(body))
					: null).ConfigureAwait(false);

			if (response.Success)
			{
				_logger.LogInformation("Successfully created index: {IndexName}", indexName);
				return true;
			}

			_logger.LogError(
				"Failed to create index: {IndexName}. Error: {Error}",
				indexName, response.Body);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create index: {IndexName}", indexName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteIndexAsync(string indexName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		try
		{
			_logger.LogInformation("Deleting index: {IndexName}", indexName);

			var response = await _client.Indices.DeleteAsync(indexName, ct: cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully deleted index: {IndexName}", indexName);
				return true;
			}

			_logger.LogError(
				"Failed to delete index: {IndexName}. Error: {Error}",
				indexName, response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete index: {IndexName}", indexName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		try
		{
			var response = await _client.Indices.ExistsAsync(indexName, ct: cancellationToken).ConfigureAwait(false);
			return response.Exists;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to check if index exists: {IndexName}", indexName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<IndexHealthStatus>> GetIndexHealthAsync(
		string? indexPattern,
		CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation("Getting index health for pattern: {Pattern}", indexPattern ?? "all");

			var response = await _client.Cluster.HealthAsync(new ClusterHealthRequest(indexPattern ?? "*"), cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				var healthStatuses = new List<IndexHealthStatus>();
				if (response.Indices != null)
				{
					foreach (var index in response.Indices)
					{
						healthStatuses.Add(new IndexHealthStatus
						{
							IndexName = index.Key.ToString(),
							Status = index.Value.Status.ToString(),
							PrimaryShards = index.Value.ActivePrimaryShards,
							ReplicaShards = index.Value.NumberOfReplicas,
							DocumentCount = 0, // Available via separate stats API if needed
							TotalSize = null, // Available via separate stats API if needed
						});
					}
				}

				return healthStatuses;
			}

			_logger.LogError("Failed to get index health. Error: {Error}", response.DebugInformation);
			return [];
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get index health for pattern: {Pattern}", indexPattern ?? "all");
			return [];
		}
	}

	/// <inheritdoc />
	public async Task<IndexOptimizationResult> OptimizeIndexAsync(string indexName, IndexOptimizationOptions optimizationOptions,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentNullException.ThrowIfNull(optimizationOptions);

		try
		{
			_logger.LogInformation("Optimizing index: {IndexName}", indexName);

			var optimizations = new List<string>();
			var warnings = new List<string>();

			// Force merge if requested
			if (optimizationOptions.ForceMerge)
			{
				var mergeSuccess = await ForceMergeAsync(indexName, optimizationOptions.TargetSegmentCount, cancellationToken)
					.ConfigureAwait(false);
				if (mergeSuccess)
				{
					optimizations.Add("Force merge completed");
				}
				else
				{
					warnings.Add("Force merge failed");
				}
			}

			return new IndexOptimizationResult
			{
				IsSuccessful = optimizations.Count != 0,
				PerformedActions = optimizations,
				Errors = warnings,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to optimize index: {IndexName}", indexName);
			return new IndexOptimizationResult
			{
				IsSuccessful = false,
				PerformedActions = [],
				Errors = [$"Optimization failed with exception: {ex.Message}"],
			};
		}
	}

	/// <inheritdoc />
	public async Task<bool> UpdateIndexSettingsAsync(string indexName, JsonElement settingsJson,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		try
		{
			_logger.LogInformation("Updating settings for index: {IndexName}", indexName);

			var request = new UpdateIndexSettingsRequest(indexName);
			var response = await _client.Indices.UpdateSettingsAsync(request, cancellationToken)
				.ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully updated settings for index: {IndexName}", indexName);
				return true;
			}

			_logger.LogError(
				"Failed to update settings for index: {IndexName}. Error: {Error}",
				indexName, response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update settings for index: {IndexName}", indexName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> ForceMergeAsync(string indexName, int? maxNumSegments, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		try
		{
			_logger.LogInformation(
				"Force merging index: {IndexName} with max segments: {MaxSegments}",
				indexName, maxNumSegments?.ToString() ?? "default");

			var response = await _client.Indices.ForceMergeAsync(indexName, f =>
			{
				if (maxNumSegments.HasValue)
				{
					_ = f.MaxNumSegments(maxNumSegments.Value);
				}

				return f;
			}, cancellationToken).ConfigureAwait(false);

			if (response.IsValid)
			{
				_logger.LogInformation("Successfully force merged index: {IndexName}", indexName);
				return true;
			}

			_logger.LogError(
				"Failed to force merge index: {IndexName}. Error: {Error}",
				indexName, response.DebugInformation);
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to force merge index: {IndexName}", indexName);
			return false;
		}
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "OpenSearch SDK types are inherently reflection-based; this adapter layer already depends on them.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "OpenSearch SDK types are inherently reflection-based; this adapter layer already depends on them.")]
	private static T? DeserializeOrNull<T>(JsonElement? element) where T : class =>
		element.HasValue ? JsonSerializer.Deserialize<T>(element.Value) : null;
}
