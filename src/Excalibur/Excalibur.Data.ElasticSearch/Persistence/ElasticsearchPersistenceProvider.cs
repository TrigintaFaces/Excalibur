// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Persistence;

/// <summary>
/// Elasticsearch implementation of <see cref="IPersistenceProvider"/> that provides
/// document persistence using Elasticsearch indices. Supports health monitoring
/// through the <see cref="IPersistenceProviderHealth"/> sub-interface via
/// <see cref="IPersistenceProvider.GetService"/>.
/// </summary>
public sealed partial class ElasticsearchPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth
{
	private readonly ElasticsearchClient _client;
	private readonly ElasticsearchPersistenceOptions _options;
	private readonly ILogger<ElasticsearchPersistenceProvider> _logger;
	private volatile bool _disposed;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchPersistenceProvider"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="options">The persistence options.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticsearchPersistenceProvider(
		ElasticsearchClient client,
		IOptions<ElasticsearchPersistenceOptions> options,
		ILogger<ElasticsearchPersistenceProvider> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Name => "Elasticsearch";

	/// <inheritdoc />
	public string ProviderType => "Search";

	/// <inheritdoc />
	public bool IsAvailable => _initialized && !_disposed;

	/// <inheritdoc />
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Elasticsearch persistence provider does not support IDataRequest<TConnection, TResult>. " +
			"Use the typed document operations (GetByIdAsync, IndexAsync, DeleteAsync, SearchAsync) instead.");
	}

	/// <inheritdoc />
	public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		_initialized = true;
		LogInitialized(_options.IndexPrefix);

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Gets a document by its identifier from the specified index.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="indexName">The index name (without prefix).</param>
	/// <param name="documentId">The document identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The document if found; otherwise <see langword="null"/>.</returns>
	public async Task<TDocument?> GetByIdAsync<TDocument>(
		string indexName,
		string documentId,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var fullIndex = GetFullIndexName(indexName);
		var response = await _client.GetAsync<TDocument>(
			fullIndex,
			new Id(documentId),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse && response.Found)
		{
			LogDocumentRetrieved(documentId, fullIndex);
			return response.Source;
		}

		return null;
	}

	/// <summary>
	/// Indexes (creates or updates) a document in the specified index.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="indexName">The index name (without prefix).</param>
	/// <param name="documentId">The document identifier.</param>
	/// <param name="document">The document to index.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation succeeded; otherwise <see langword="false"/>.</returns>
	public async Task<bool> IndexAsync<TDocument>(
		string indexName,
		string documentId,
		TDocument document,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		var fullIndex = GetFullIndexName(indexName);
		var response = await _client.IndexAsync(
			document,
			idx => idx.Index(fullIndex).Id(documentId).Refresh(MapRefreshPolicy()),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			LogDocumentIndexed(documentId, fullIndex);
			return true;
		}

		LogIndexFailed(documentId, fullIndex, response.DebugInformation);
		return false;
	}

	/// <summary>
	/// Deletes a document by its identifier from the specified index.
	/// </summary>
	/// <param name="indexName">The index name (without prefix).</param>
	/// <param name="documentId">The document identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the document was deleted; otherwise <see langword="false"/>.</returns>
	public async Task<bool> DeleteAsync(
		string indexName,
		string documentId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var fullIndex = GetFullIndexName(indexName);
		var response = await _client.DeleteAsync(
			fullIndex,
			new Id(documentId),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			LogDocumentDeleted(documentId, fullIndex);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Searches for documents in the specified index using a query string.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="indexName">The index name (without prefix).</param>
	/// <param name="queryString">The Elasticsearch query string.</param>
	/// <param name="maxResults">The maximum number of results to return.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A read-only list of matching documents.</returns>
	public async Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
		string indexName,
		string queryString,
		int? maxResults,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
		ArgumentException.ThrowIfNullOrWhiteSpace(queryString);

		var fullIndex = GetFullIndexName(indexName);
		var size = Math.Min(maxResults ?? _options.MaxResultCount, _options.MaxResultCount);

		var response = await _client.SearchAsync<TDocument>(
			s => s.Index(fullIndex)
				.QueryLuceneSyntax(queryString)
				.Size(size),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			LogSearchCompleted(fullIndex, response.Documents.Count);
			return response.Documents.ToList().AsReadOnly();
		}

		return Array.Empty<TDocument>();
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			var response = await _client.PingAsync(cancellationToken).ConfigureAwait(false);
			return response.IsValidResponse;
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["provider"] = Name,
			["providerType"] = ProviderType,
			["indexPrefix"] = _options.IndexPrefix,
			["isAvailable"] = IsAvailable
		};

		try
		{
			var healthResponse = await _client.Cluster.HealthAsync(cancellationToken).ConfigureAwait(false);
			if (healthResponse.IsValidResponse)
			{
				metrics["clusterStatus"] = healthResponse.Status.ToString();
				metrics["numberOfNodes"] = healthResponse.NumberOfNodes;
				metrics["activeShards"] = healthResponse.ActiveShards;
			}
		}
		catch (Exception ex)
		{
			metrics["healthCheckError"] = ex.Message;
		}

		return metrics;
	}

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		// Elasticsearch client manages connection pooling internally;
		// pool stats are not directly exposed.
		return Task.FromResult<IDictionary<string, object>?>(null);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}

	private string GetFullIndexName(string indexName) =>
		$"{_options.IndexPrefix}{indexName}";

	private Refresh MapRefreshPolicy() =>
		_options.RefreshPolicy switch
		{
			ElasticsearchRefreshPolicy.Immediate => Refresh.True,
			ElasticsearchRefreshPolicy.WaitFor => Refresh.WaitFor,
			_ => Refresh.False,
		};

	[LoggerMessage(3400, LogLevel.Information, "Elasticsearch persistence provider initialized with index prefix '{IndexPrefix}'")]
	private partial void LogInitialized(string indexPrefix);

	[LoggerMessage(3401, LogLevel.Debug, "Retrieved document '{DocumentId}' from index '{IndexName}'")]
	private partial void LogDocumentRetrieved(string documentId, string indexName);

	[LoggerMessage(3402, LogLevel.Debug, "Indexed document '{DocumentId}' in index '{IndexName}'")]
	private partial void LogDocumentIndexed(string documentId, string indexName);

	[LoggerMessage(3403, LogLevel.Warning, "Failed to index document '{DocumentId}' in index '{IndexName}': {DebugInfo}")]
	private partial void LogIndexFailed(string documentId, string indexName, string debugInfo);

	[LoggerMessage(3404, LogLevel.Debug, "Deleted document '{DocumentId}' from index '{IndexName}'")]
	private partial void LogDocumentDeleted(string documentId, string indexName);

	[LoggerMessage(3405, LogLevel.Debug, "Search completed on index '{IndexName}', returned {Count} documents")]
	private partial void LogSearchCompleted(string indexName, int count);

	[LoggerMessage(3406, LogLevel.Warning, "Elasticsearch connection test failed")]
	private partial void LogConnectionTestFailed(Exception exception);
}
