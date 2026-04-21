// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Reindex;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Excalibur.Data.ElasticSearch.Internal;

/// <summary>
/// Default <see cref="ISchemaEvolutionOperations"/> implementation that
/// forwards to <c>_inner.ReindexAsync</c>, <c>_inner.Indices.PutMappingAsync</c>,
/// <c>_inner.Indices.GetMappingAsync</c>, <c>_inner.Indices.ExistsAsync</c>,
/// and <c>_inner.Indices.CreateAsync</c> on a real
/// <see cref="ElasticsearchClient"/>. Owns the SDK descriptor construction +
/// mapping-projection logic so the seam consumer sees only domain-shaped
/// types.
/// </summary>
internal sealed class SchemaEvolutionOperationsAdapter : ISchemaEvolutionOperations
{
	private const string DefaultSchemaVersion = "1.0.0";

	private readonly ElasticsearchClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="SchemaEvolutionOperationsAdapter"/> class.
	/// </summary>
	public SchemaEvolutionOperationsAdapter(ElasticsearchClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task<MigrationStepOutcome> MigrateAsync(
		string sourceIndex,
		string targetIndex,
		object? mapping,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceIndex);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetIndex);

		// UpdateInPlace shape (source == target): skip the reindex entirely and
		// apply the mapping directly to the single index. A self-reindex would
		// fail on real Elasticsearch; the MigrationStrategy.UpdateInPlace contract
		// is "apply new mapping to existing index without moving documents."
		// See SENTINEL msg 1956 / OVERWATCH msg 1959 (Path B, criterion 2).
		var isInPlace = string.Equals(sourceIndex, targetIndex, StringComparison.Ordinal);

		if (!isInPlace)
		{
			var reindexRequest = new ReindexRequest
			{
				Source = new Source { Indices = sourceIndex },
				Dest = new Destination { Index = targetIndex },
				Refresh = true,
				WaitForCompletion = true,
			};

			var reindexResponse = await _inner.ReindexAsync(reindexRequest, cancellationToken)
				.ConfigureAwait(false);

			if (!reindexResponse.IsValidResponse)
			{
				return new MigrationStepOutcome(
					false,
					reindexResponse.ElasticsearchServerError?.Error?.Reason);
			}
		}

		if (mapping is TypeMapping tm && tm.Properties is not null)
		{
			var putRequest = new PutMappingRequest(targetIndex) { Properties = tm.Properties };
			var putResponse = await _inner.Indices.PutMappingAsync(putRequest, cancellationToken)
				.ConfigureAwait(false);

			if (!putResponse.IsValidResponse)
			{
				return new MigrationStepOutcome(
					false,
					putResponse.ElasticsearchServerError?.Error?.Reason);
			}
		}

		return new MigrationStepOutcome(true, null);
	}

	/// <inheritdoc/>
	public async Task<SchemaVersion?> VerifyVersionAsync(
		string indexName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		GetMappingResponse response;
		try
		{
			response = await _inner.Indices
				.GetMappingAsync(new GetMappingRequest(indexName), cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Elastic.Transport.TransportException)
		{
			// ES 9 throws UnexpectedTransportException (e.g. JSON deserialization
			// failure) when the target index does not exist. Treat as "no version
			// information available" — the soft-fail null contract.
			return null;
		}

		return ExtractSchemaVersion(response, indexName, allowDefault: false);
	}

	/// <inheritdoc/>
	public async Task<MigrationStepOutcome> RollbackAsync(
		string sourceIndex,
		string targetIndex,
		object? mapping,
		CancellationToken cancellationToken)
	{
		// Rollback = forward migration with source/target swapped.
		return await MigrateAsync(targetIndex, sourceIndex, mapping, cancellationToken)
			.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<SchemaVersion> GetSchemaVersionAsync(
		string indexName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		var response = await _inner.Indices
			.GetMappingAsync(new GetMappingRequest(indexName), cancellationToken)
			.ConfigureAwait(false);

		return ExtractSchemaVersion(response, indexName, allowDefault: true)
			?? new SchemaVersion(DefaultSchemaVersion, null);
	}

	/// <inheritdoc/>
	public async Task<MigrationStepOutcome> EnsureMigrationIndexAsync(
		string indexName,
		object? mapping,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

		var exists = await _inner.Indices.ExistsAsync(indexName, cancellationToken)
			.ConfigureAwait(false);

		if (exists.Exists)
		{
			return new MigrationStepOutcome(true, null);
		}

		var createRequest = new CreateIndexRequest(indexName)
		{
			Mappings = mapping as TypeMapping,
			Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
		};

		var response = await _inner.Indices.CreateAsync(createRequest, cancellationToken)
			.ConfigureAwait(false);

		return new MigrationStepOutcome(
			response.IsValidResponse,
			response.IsValidResponse ? null : response.ElasticsearchServerError?.Error?.Reason);
	}

	private static SchemaVersion? ExtractSchemaVersion(
		GetMappingResponse response,
		string indexName,
		bool allowDefault)
	{
		if (!response.IsValidResponse || !response.Mappings.TryGetValue(indexName, out var mapping))
		{
			return null;
		}

		var properties = mapping.Mappings?.Properties;
		if (properties is null)
		{
			return allowDefault
				? new SchemaVersion(DefaultSchemaVersion, null)
				: null;
		}

		var fieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var kvp in properties)
		{
			fieldTypes[kvp.Key.ToString()] = GetPropertyTypeName(kvp.Value);
		}

#pragma warning disable IL2026, IL3050 // JSON serialization of internal dictionary; type-safe shape
		var mappingJson = JsonSerializer.Serialize(fieldTypes);
#pragma warning restore IL2026, IL3050

		// Today no SchemaEvolutionHandler call site stamps a version into the
		// mapping. Return the legacy default so consumers behave identically.
		return new SchemaVersion(DefaultSchemaVersion, mappingJson);
	}

	private static string GetPropertyTypeName(IProperty property)
	{
		var typeName = property.GetType().Name;
		return typeName.EndsWith("Property", StringComparison.Ordinal)
			? typeName[..^"Property".Length]
			: typeName;
	}
}
