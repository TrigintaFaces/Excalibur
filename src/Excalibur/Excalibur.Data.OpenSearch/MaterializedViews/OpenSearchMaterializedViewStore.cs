// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.Data.OpenSearch.Diagnostics;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Data.OpenSearch.MaterializedViews;

/// <summary>
/// OpenSearch implementation of <see cref="IMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores materialized views as JSON documents in OpenSearch with the following schema:
/// <list type="bullet">
/// <item>Views index for view data with document ID as viewName:viewId</item>
/// <item>Positions index for position tracking with document ID as viewName</item>
/// </list>
/// </para>
/// <para>
/// Uses upsert operations for thread-safe save operations.
/// </para>
/// </remarks>
public sealed partial class OpenSearchMaterializedViewStore : IMaterializedViewStore, IAsyncDisposable
{
	private readonly OpenSearchMaterializedViewStoreOptions _options;
	private readonly ILogger<OpenSearchMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private OpenSearchClient? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public OpenSearchMaterializedViewStore(
		IOptions<OpenSearchMaterializedViewStoreOptions> options,
		ILogger<OpenSearchMaterializedViewStore> logger,
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
	/// Initializes a new instance of the <see cref="OpenSearchMaterializedViewStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing OpenSearch client.</param>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public OpenSearchMaterializedViewStore(
		OpenSearchClient client,
		IOptions<OpenSearchMaterializedViewStoreOptions> options,
		ILogger<OpenSearchMaterializedViewStore> logger,
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

		var response = await _client!.GetAsync<MaterializedViewDocument>(
			documentId,
			g => g.Index(_options.ViewsIndexName),
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

		var response = await _client!.IndexAsync(
			document,
			idx => idx
				.Index(_options.ViewsIndexName)
				.Id(documentId)
				.Refresh(Refresh.True),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValid)
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

		var response = await _client!.DeleteAsync<MaterializedViewDocument>(
			documentId,
			d => d.Index(_options.ViewsIndexName),
			cancellationToken).ConfigureAwait(false);

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

		var response = await _client!.GetAsync<MaterializedViewPositionDocument>(
			viewName,
			g => g.Index(_options.PositionsIndexName),
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

		var response = await _client!.IndexAsync(
			document,
			idx => idx
				.Index(_options.PositionsIndexName)
				.Id(viewName)
				.Refresh(Refresh.True),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValid)
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
		// OpenSearchClient doesn't implement IDisposable - it manages connections internally
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
#pragma warning disable CA2000 // ConnectionSettings lifetime managed by OpenSearchClient
			var settings = new ConnectionSettings(new Uri(_options.NodeUri))
				.RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
#pragma warning restore CA2000

			if (!string.IsNullOrWhiteSpace(_options.Auth.Username) && !string.IsNullOrWhiteSpace(_options.Auth.Password))
			{
				settings = settings.BasicAuthentication(_options.Auth.Username, _options.Auth.Password);
			}

			if (_options.EnableDebugMode)
			{
				settings = settings.DisableDirectStreaming();
			}

			_client = new OpenSearchClient(settings);
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
		var existsResponse = await _client!.Indices.ExistsAsync(
			_options.ViewsIndexName,
			ct: cancellationToken).ConfigureAwait(false);

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
				.Map(m => m
					.Properties(p => p
						.Keyword(k => k.Name("viewName"))
						.Keyword(k => k.Name("viewId"))
						.Text(t => t.Name("data").Index(false))
						.Date(d => d.Name("createdAt"))
						.Date(d => d.Name("updatedAt")))),
			cancellationToken).ConfigureAwait(false);

		if (!createResponse.IsValid)
		{
			throw new InvalidOperationException(
				$"Failed to create views index: {createResponse.DebugInformation}");
		}
	}

	private async Task EnsurePositionsIndexExistsAsync(CancellationToken cancellationToken)
	{
		var existsResponse = await _client!.Indices.ExistsAsync(
			_options.PositionsIndexName,
			ct: cancellationToken).ConfigureAwait(false);

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
				.Map(m => m
					.Properties(p => p
						.Keyword(k => k.Name("viewName"))
						.Number(n => n.Name("position").Type(NumberType.Long))
						.Date(d => d.Name("createdAt"))
						.Date(d => d.Name("updatedAt")))),
			cancellationToken).ConfigureAwait(false);

		if (!createResponse.IsValid)
		{
			throw new InvalidOperationException(
				$"Failed to create positions index: {createResponse.DebugInformation}");
		}
	}

	#region Logging

	[LoggerMessage(
		EventId = DataOpenSearchEventId.DocumentRetrieved,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} loaded")]
	private partial void LogViewLoaded(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataOpenSearchEventId.DocumentNotFound,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} not found")]
	private partial void LogViewNotFound(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataOpenSearchEventId.DocumentIndexed,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} saved")]
	private partial void LogViewSaved(string viewName, string viewId);

	[LoggerMessage(
		EventId = DataOpenSearchEventId.DocumentDeleted,
		Level = LogLevel.Debug,
		Message = "View {ViewName}/{ViewId} deleted")]
	private partial void LogViewDeleted(string viewName, string viewId);

	[LoggerMessage(
		EventId = 108206,
		Level = LogLevel.Debug,
		Message = "Position for {ViewName} loaded: {Position}")]
	private partial void LogPositionLoaded(string viewName, long position);

	[LoggerMessage(
		EventId = 108207,
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
