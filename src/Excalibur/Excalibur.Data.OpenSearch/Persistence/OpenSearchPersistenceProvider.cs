// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Data.OpenSearch.Persistence;

/// <summary>
/// OpenSearch implementation of <see cref="IPersistenceProvider"/> that provides
/// document persistence using OpenSearch indices. Supports health monitoring
/// through the <see cref="IPersistenceProviderHealth"/> sub-interface via
/// <see cref="IPersistenceProvider.GetService"/>.
/// </summary>
public sealed partial class OpenSearchPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth
{
	private readonly OpenSearchClient _client;
	private readonly OpenSearchPersistenceOptions _options;
	private readonly ILogger<OpenSearchPersistenceProvider> _logger;
	private volatile bool _disposed;
	private volatile bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchPersistenceProvider"/> class.
	/// </summary>
	/// <param name="client">The OpenSearch client.</param>
	/// <param name="options">The persistence options.</param>
	/// <param name="logger">The logger instance.</param>
	public OpenSearchPersistenceProvider(
		OpenSearchClient client,
		IOptions<OpenSearchPersistenceOptions> options,
		ILogger<OpenSearchPersistenceProvider> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		Name = string.IsNullOrWhiteSpace(_options.Name) ? "opensearch" : _options.Name;
	}

	/// <inheritdoc />
	/// <remarks>
	/// The consumer-configured instance name, taken from the options and defaulting to
	/// <c>"opensearch"</c>. This identifies which configured instance answered; the engine
	/// identity is reported separately under the <c>Provider</c> metrics key.
	/// </remarks>
	public string Name { get; }

	/// <inheritdoc />
	public string ProviderType => "Search";

	/// <inheritdoc />
	public bool IsAvailable => _initialized && !_disposed;

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">
	/// The cluster could not be reached. The provider is left unavailable rather than reporting a
	/// readiness it has not established.
	/// </exception>
	/// <remarks>
	/// Reachability is verified before the provider reports itself available, so <see cref="IsAvailable" />
	/// reflects a connection this provider actually made rather than merely the fact that it was asked to
	/// initialize.
	/// </remarks>
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!await TestConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException(
				$"Failed to initialize OpenSearch provider '{Name}': Connection test failed");
		}

		_initialized = true;
		LogInitialized(_options.IndexPrefix);
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
			documentId,
			g => g.Index(fullIndex),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValid && response.Found)
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

		if (response.IsValid)
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
		var response = await _client.DeleteAsync<object>(
			documentId,
			d => d.Index(fullIndex),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValid)
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
	/// <param name="queryString">The OpenSearch query string.</param>
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
				.Query(q => q.QueryString(qs => qs.Query(queryString)))
				.Size(size),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValid)
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
			var response = await _client.PingAsync(p => p, cancellationToken).ConfigureAwait(false);

			// A verified round-trip IS the readiness this provider reports: latch it here so a provider used
			// the way its own registration builds it -- without an explicit InitializeAsync, which that
			// registration never performs -- does not report itself unavailable while demonstrably working.
			_initialized = _initialized || response.IsValid;

			return response.IsValid;
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
			// PascalCase, matching every other persistence provider: this dictionary is compared with an
			// ordinal comparer, so "provider" and "Provider" are different keys. "Provider" is the stable
			// engine identity -- a fixed literal -- and "Name" the consumer-configured instance name, which
			// is what distinguishes two instances of the same engine.
			["Provider"] = "OpenSearch",
			["Name"] = Name,
			["ProviderType"] = ProviderType,
			["IndexPrefix"] = _options.IndexPrefix,
			["IsAvailable"] = IsAvailable
		};

		try
		{
			var healthResponse = await _client.Cluster.HealthAsync(
				new ClusterHealthRequest(),
				cancellationToken).ConfigureAwait(false);

			if (healthResponse.IsValid)
			{
				metrics["ClusterStatus"] = healthResponse.Status.ToString();
				metrics["NumberOfNodes"] = healthResponse.NumberOfNodes;
				metrics["ActiveShards"] = healthResponse.ActiveShards;
			}
		}
		catch (Exception ex)
		{
			metrics["HealthCheckError"] = ex.Message;
		}

		return metrics;
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
			OpenSearchRefreshPolicy.Immediate => Refresh.True,
			OpenSearchRefreshPolicy.WaitFor => Refresh.WaitFor,
			_ => Refresh.False,
		};

	[LoggerMessage(3450, LogLevel.Information, "OpenSearch persistence provider initialized with index prefix '{IndexPrefix}'")]
	private partial void LogInitialized(string indexPrefix);

	[LoggerMessage(3451, LogLevel.Debug, "Retrieved document '{DocumentId}' from index '{IndexName}'")]
	private partial void LogDocumentRetrieved(string documentId, string indexName);

	[LoggerMessage(3452, LogLevel.Debug, "Indexed document '{DocumentId}' in index '{IndexName}'")]
	private partial void LogDocumentIndexed(string documentId, string indexName);

	[LoggerMessage(3453, LogLevel.Warning, "Failed to index document '{DocumentId}' in index '{IndexName}': {DebugInfo}")]
	private partial void LogIndexFailed(string documentId, string indexName, string debugInfo);

	[LoggerMessage(3454, LogLevel.Debug, "Deleted document '{DocumentId}' from index '{IndexName}'")]
	private partial void LogDocumentDeleted(string documentId, string indexName);

	[LoggerMessage(3455, LogLevel.Debug, "Search completed on index '{IndexName}', returned {Count} documents")]
	private partial void LogSearchCompleted(string indexName, int count);

	[LoggerMessage(3456, LogLevel.Warning, "OpenSearch connection test failed")]
	private partial void LogConnectionTestFailed(Exception exception);
}
