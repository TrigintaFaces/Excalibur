// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.MaterializedViews;

/// <summary>
/// Elasticsearch implementation of <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores materialized views as JSON documents in Elasticsearch with the following schema:
/// <list type="bullet">
/// <item>Views index for view data with document ID as viewName:viewId</item>
/// <item>Positions index for position tracking with document ID as viewName</item>
/// </list>
/// </para>
/// <para>
/// Uses upsert operations for thread-safe save operations.
/// </para>
/// </remarks>
public sealed partial class ElasticSearchMaterializedViewStore : IMaterializedViewStore, IAsyncDisposable
{
	private readonly ElasticSearchMaterializedViewStoreOptions _options;
	private readonly ILogger<ElasticSearchMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private ElasticsearchClient? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public ElasticSearchMaterializedViewStore(
		IOptions<ElasticSearchMaterializedViewStoreOptions> options,
		ILogger<ElasticSearchMaterializedViewStore> logger,
		JsonSerializerOptions? jsonOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = jsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchMaterializedViewStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing Elasticsearch client.</param>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public ElasticSearchMaterializedViewStore(
		ElasticsearchClient client,
		IOptions<ElasticSearchMaterializedViewStoreOptions> options,
		ILogger<ElasticSearchMaterializedViewStore> logger,
		JsonSerializerOptions? jsonOptions = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_jsonOptions = jsonOptions ?? new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON deserialization might require runtime code generation.")]
	public async ValueTask<TView?> GetAsync<TView>(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
		where TView : class
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(viewName, viewId);

		var response = await _client.GetAsync<MaterializedViewDocument>(
			_options.ViewsIndexName,
			documentId,
			cancellationToken).ConfigureAwait(false);

		if (!response.Found || response.Source == null)
		{
			LogViewNotFound(viewName, viewId);
			return null;
		}

		LogViewLoaded(viewName, viewId);

		// Deserialize the view data from JSON string
		return JsonSerializer.Deserialize<TView>(response.Source.Data, _jsonOptions);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization might require runtime code generation.")]
	public async ValueTask SaveAsync<TView>(
		string viewName,
		string viewId,
		TView view,
		CancellationToken cancellationToken)
		where TView : class
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		ArgumentNullException.ThrowIfNull(view);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(viewName, viewId);
		var now = DateTimeOffset.UtcNow;

		var document = new MaterializedViewDocument
		{
			ViewName = viewName,
			ViewId = viewId,
			Data = JsonSerializer.Serialize(view, _jsonOptions),
			CreatedAt = now,
			UpdatedAt = now
		};

		var response = await _client.IndexAsync(
			document,
			idx => idx
				.Index(_options.ViewsIndexName)
				.Id(documentId)
				.Refresh(Refresh.True),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to save materialized view {viewName}/{viewId}: {response.DebugInformation}");
		}

		LogViewSaved(viewName, viewId);
	}

	/// <inheritdoc/>
	public async ValueTask DeleteAsync(
		string viewName,
		string viewId,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CreateDocumentId(viewName, viewId);

		var deleteRequest = new DeleteRequest(_options.ViewsIndexName, new Id(documentId));
		var response = await _client
			.DeleteAsync(deleteRequest, cancellationToken)
			.ConfigureAwait(false);

		if (response.Result == Result.Deleted)
		{
			LogViewDeleted(viewName, viewId);
		}
	}

	/// <inheritdoc/>
	public async ValueTask<long?> GetPositionAsync(
		string viewName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.GetAsync<MaterializedViewPositionDocument>(
			_options.PositionsIndexName,
			viewName,
			cancellationToken).ConfigureAwait(false);

		if (!response.Found || response.Source == null)
		{
			return null;
		}

		LogPositionLoaded(viewName, response.Source.Position);
		return response.Source.Position;
	}

	/// <inheritdoc/>
	public async ValueTask SavePositionAsync(
		string viewName,
		long position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		var document = new MaterializedViewPositionDocument { ViewName = viewName, Position = position, CreatedAt = now, UpdatedAt = now };

		var response = await _client.IndexAsync(
			document,
			idx => idx
				.Index(_options.PositionsIndexName)
				.Id(viewName)
				.Refresh(Refresh.True),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to save position for {viewName}: {response.DebugInformation}");
		}

		LogPositionSaved(viewName, position);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		// ElasticsearchClient doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	private static string CreateDocumentId(string viewName, string viewId) => $"{viewName}:{viewId}";

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client == null)
		{
			var settings = new ElasticsearchClientSettings(new Uri(_options.NodeUri))
				.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

			if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
			{
				settings = settings.Authentication(new BasicAuthentication(_options.Username, _options.Password));
			}
			else if (!string.IsNullOrWhiteSpace(_options.ApiKey))
			{
				settings = settings.Authentication(new ApiKey(_options.ApiKey));
			}

			if (_options.EnableDebugMode)
			{
				settings = settings.DisableDirectStreaming();
			}

			_client = new ElasticsearchClient(settings);
		}

		if (_options.CreateIndexOnInitialize)
		{
			await EnsureViewsIndexExistsAsync(cancellationToken).ConfigureAwait(false);
			await EnsurePositionsIndexExistsAsync(cancellationToken).ConfigureAwait(false);
		}

		_initialized = true;
	}

	private async Task EnsureViewsIndexExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices.ExistsAsync(
			_options.ViewsIndexName,
			cancellationToken).ConfigureAwait(false);

		if (existsResponse.Exists)
		{
			return;
		}

		var createResponse = await _client.Indices.CreateAsync(
			_options.ViewsIndexName,
			c => c
				.Settings(s => s
					.NumberOfShards(_options.NumberOfShards)
					.NumberOfReplicas(_options.NumberOfReplicas)
					.RefreshInterval(_options.RefreshInterval))
				.Mappings(m => m
					.Properties(new Properties
					{
						{ "viewName", new KeywordProperty() },
						{ "viewId", new KeywordProperty() },
						{ "data", new TextProperty { Index = false } },
						{ "createdAt", new DateProperty() },
						{ "updatedAt", new DateProperty() }
					})),
			cancellationToken).ConfigureAwait(false);

		if (!createResponse.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to create views index: {createResponse.DebugInformation}");
		}
	}

	private async Task EnsurePositionsIndexExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client.Indices.ExistsAsync(
			_options.PositionsIndexName,
			cancellationToken).ConfigureAwait(false);

		if (existsResponse.Exists)
		{
			return;
		}

		var createResponse = await _client.Indices.CreateAsync(
			_options.PositionsIndexName,
			c => c
				.Settings(s => s
					.NumberOfShards(_options.NumberOfShards)
					.NumberOfReplicas(_options.NumberOfReplicas)
					.RefreshInterval(_options.RefreshInterval))
				.Mappings(m => m
					.Properties(new Properties
					{
						{ "viewName", new KeywordProperty() },
						{ "position", new LongNumberProperty() },
						{ "createdAt", new DateProperty() },
						{ "updatedAt", new DateProperty() }
					})),
			cancellationToken).ConfigureAwait(false);

		if (!createResponse.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to create positions index: {createResponse.DebugInformation}");
		}
	}

	#region Logging

	[LoggerMessage(
		EventId = DataElasticsearchEventId.DocumentRetrieved,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} loaded")]
	private partial void LogViewLoaded(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataElasticsearchEventId.DocumentNotFound,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} not found")]
	private partial void LogViewNotFound(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataElasticsearchEventId.DocumentIndexed,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} saved")]
	private partial void LogViewSaved(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataElasticsearchEventId.DocumentDeleted,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} deleted")]
	private partial void LogViewDeleted(string viewName, string viewId);

	[LoggerMessage(
		EventId = 106206,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} loaded: {Position}")]
	private partial void LogPositionLoaded(string viewName, long position);

	[LoggerMessage(
		EventId = 106207,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} saved: {Position}")]
	private partial void LogPositionSaved(string viewName, long position);

	#endregion Logging

	#region Internal Document Types

	/// <summary>
	/// Internal document model for materialized views.
	/// </summary>
	internal sealed class MaterializedViewDocument
	{
		public string ViewName { get; set; } = string.Empty;
		public string ViewId { get; set; } = string.Empty;
		public string Data { get; set; } = string.Empty;
		public DateTimeOffset CreatedAt { get; set; }
		public DateTimeOffset UpdatedAt { get; set; }
	}

	/// <summary>
	/// Internal document model for position tracking.
	/// </summary>
	internal sealed class MaterializedViewPositionDocument
	{
		public string ViewName { get; set; } = string.Empty;
		public long Position { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
		public DateTimeOffset UpdatedAt { get; set; }
	}

	#endregion Internal Document Types
}
