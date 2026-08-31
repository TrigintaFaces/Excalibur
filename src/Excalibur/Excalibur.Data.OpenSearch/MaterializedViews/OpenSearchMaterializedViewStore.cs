// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data.OpenSearch.Diagnostics;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;
using OpenSearch.Net;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

using Excalibur.Dispatch;

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
/// <para>
/// Does not implement <see cref="Excalibur.EventSourcing.IAtomicMaterializedViewStore"/>: a view and its
/// checkpoint live in different indices and there is no cross-index transaction to commit them together, so
/// this store cannot offer exactly-once projection. Wiring it to a projection that requires exactly-once is
/// refused at startup rather than degrading to at-least-once in production.
/// </para>
/// <para>
/// Views and checkpoints are partitioned by tenant. The tenant term is part of each document's
/// identifier rather than a filter applied over it, so two tenants projecting the same named view hold
/// distinct documents and distinct checkpoints. Keyed on view name and view id alone they shared one
/// document: the later writer's data silently replaced the earlier one's, and a read returned whichever
/// tenant wrote last. The checkpoint was worse -- keyed on view name alone it held ONE position for
/// every tenant, so one tenant's progress advanced another's and that tenant's projector skipped every
/// event in between, permanently.
/// </para>
/// <para>
/// A document written before this partitioning existed carries the un-prefixed identifier, so a scoped
/// read does not find it. That direction is the safe one and it is chosen deliberately: an unfound
/// checkpoint reads as unset, and an unset checkpoint replays from the beginning, which re-derives the
/// view. The alternative failure -- a tenant inheriting a checkpoint written by another -- would skip
/// the events in between with no error and no way to detect it afterwards. Replay costs time; a skip
/// costs data.
/// </para>
/// </remarks>
public sealed partial class OpenSearchMaterializedViewStore : IMaterializedViewStore, IAsyncDisposable
{
	private readonly OpenSearchMaterializedViewStoreOptions _options;
	private readonly ILogger<OpenSearchMaterializedViewStore> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private OpenSearchClient? _client;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	private readonly ITenantContext _tenantContext;

	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every identifier it builds
	/// uses the same value. The context is a required dependency, so the term is decided identically on
	/// every path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchMaterializedViewStore"/> class.
	/// </summary>
	/// <param name="options">The store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions documents by tenant, and it resolves
	/// that partition from here, so there is no state in which the partition is undecided. A single-tenant
	/// host receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public OpenSearchMaterializedViewStore(
		IOptions<OpenSearchMaterializedViewStoreOptions> options,
		ILogger<OpenSearchMaterializedViewStore> logger,
		ITenantContext tenantContext,
		JsonSerializerOptions? jsonOptions = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		// Read-model serialization — intentionally NOT the event canonical contract (a view is not an event;
		// consumer-injectable). The numeric-enum representation is preserved: this JSON is a queryable,
		// consumer-facing surface (SQL/search filters) where enum-as-string would break range/equality queries.
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
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions documents by tenant, and it resolves
	/// that partition from here, so there is no state in which the partition is undecided. A single-tenant
	/// host receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="jsonOptions">Optional JSON serializer options.</param>
	public OpenSearchMaterializedViewStore(
		OpenSearchClient client,
		IOptions<OpenSearchMaterializedViewStoreOptions> options,
		ILogger<OpenSearchMaterializedViewStore> logger,
		ITenantContext tenantContext,
		JsonSerializerOptions? jsonOptions = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tenantContext);

		_tenantContext = tenantContext;

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		// Read-model serialization — intentionally NOT the event canonical contract (a view is not an event;
		// consumer-injectable). The numeric-enum representation is preserved: this JSON is a queryable,
		// consumer-facing surface (SQL/search filters) where enum-as-string would break range/equality queries.
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

		// SaveAsync is a full-document IndexAsync overwrite, so the original CreatedAt must be
		// carried forward on update — re-read the existing document and preserve its CreatedAt,
		// falling back to now for a first insert (or an unreadable prior document).
		var existing = await _client!.GetAsync<MaterializedViewDocument>(
			documentId,
			g => g.Index(_options.ViewsIndexName),
			cancellationToken).ConfigureAwait(false);
		var createdAt = existing is { Found: true, Source: not null }
			? existing.Source.CreatedAt
			: now;

		var document = new MaterializedViewDocument
		{
			TenantId = CurrentTenantPartition.TenantId,
			ViewName = viewName,
			ViewId = viewId,
			Data = JsonSerializer.Serialize(view, _jsonOptions),
			CreatedAt = createdAt,
			UpdatedAt = now
		};

		var response = await _client!.IndexAsync(
			document,
			idx => idx
				.Index(_options.ViewsIndexName)
				.Id(documentId)
				.Refresh(GetRefresh()),
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
			CreatePositionDocumentId(viewName),
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

		var document = new MaterializedViewPositionDocument
		{
			TenantId = CurrentTenantPartition.TenantId,
			ViewName = viewName,
			Position = position,
			CreatedAt = now,
			UpdatedAt = now
		};

		// Monotonic advance, enforced by the store rather than by the caller. The checkpoint is written under
		// external versioning: OpenSearch accepts the write only when the supplied version is greater than or
		// equal to the stored one, and answers 409 otherwise. A delayed or retried write carrying an older
		// position is therefore rejected instead of rewinding the checkpoint and replaying applied events.
		//
		// The version is the position offset by one because external versions must be positive, and position
		// zero is a legitimate starting checkpoint. Versioning is index metadata, so this does not depend on
		// how the document's fields happen to be serialized.
		var response = await _client!.IndexAsync(
			document,
			idx => idx
				.Index(_options.PositionsIndexName)
				.Id(CreatePositionDocumentId(viewName))
				.Version(position + 1)
				.VersionType(VersionType.ExternalGte)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValid)
		{
			// A stale write losing the race is the guard working, not a failure: a higher checkpoint is
			// already durable, and re-applying this one would move it backwards.
			if (response.ApiCall?.HttpStatusCode == 409)
			{
				return;
			}

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

		// Disposed AFTER _disposed is set, and the ordering is the whole point. _disposed is what
		// stops a caller reaching WaitAsync/Release, so destroying the semaphore first creates an
		// interval where the guard is gone but callers are still admitted. In that interval an
		// in-flight initialiser's Release() throws ObjectDisposedException from its finally --
		// replacing whatever the try produced, including the real diagnostic -- and any caller
		// already blocked in WaitAsync is never signalled at all.
		//
		// The earlier comment here claimed disposing first meant "a throw later still frees the
		// handle". That was backwards: it does not protect against a later throw, it maximises the
		// window in which the initialiser's Release is guaranteed to throw. try/finally is what
		// frees a handle on a throw.
		_initLock?.Dispose();
		// OpenSearchClient doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Prefixes a document identifier with the ambient tenant, confining it to that tenant's partition.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tenant segment is length-prefixed rather than merely delimited. A tenant identifier may
	/// legally contain the delimiter, and without the prefix ("a", "b:c") and ("a:b", "c") compose to
	/// the SAME identifier -- a cross-tenant collision reintroduced by the very code meant to prevent
	/// one. The prefix makes the segment self-delimiting, so no two distinct tenants can produce the
	/// same identifier.
	/// </para>
	/// <para>
	/// A printable prefix is used rather than the ASCII unit separator the in-process cursor store
	/// uses for the same purpose, because this identifier travels in a URL path.
	/// </para>
	/// <para>
	/// The term is always present. <see cref="KeyedTenantPartition"/> has no empty inhabitant, so an
	/// unscoped host binds the reserved untenanted sentinel rather than omitting the segment: "this
	/// deployment has no tenants" and "somebody forgot to supply one" cannot become the same document.
	/// </para>
	/// </remarks>
	/// <param name="key">The un-partitioned document identifier.</param>
	/// <returns>The identifier confined to the ambient tenant's partition.</returns>
	private string QualifyWithTenant(string key)
	{
		var tenantId = CurrentTenantPartition.TenantId;
		return string.Create(
			CultureInfo.InvariantCulture,
			$"t{tenantId.Length}:{tenantId}:{key}");
	}

	private string CreateDocumentId(string viewName, string viewId) =>
		QualifyWithTenant($"{viewName}:{viewId}");

	/// <summary>
	/// Builds the checkpoint document identifier for a view, confined to the ambient tenant.
	/// </summary>
	/// <param name="viewName">The view whose checkpoint is addressed.</param>
	/// <returns>The tenant-partitioned checkpoint identifier.</returns>
	private string CreatePositionDocumentId(string viewName) =>
		QualifyWithTenant(viewName);

	private Refresh GetRefresh() =>
		_options.RefreshPolicy == "true" ? Refresh.True
		: _options.RefreshPolicy == "false" ? Refresh.False
		: Refresh.WaitFor;

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}


		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Re-check inside the lock: the winner finished while this caller waited.
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
		finally
		{
			_ = _initLock.Release();
		}
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
						.Keyword(k => k.Name("tenantId"))
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
						.Keyword(k => k.Name("tenantId"))
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
		/// <summary>
		/// The owning tenant. The identifier already confines the document to its partition; this field
		/// makes that partition visible to a query and to an operator reading the index.
		/// </summary>
		public string TenantId { get; set; } = string.Empty;
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
		/// <summary>
		/// The owning tenant. The identifier already confines the checkpoint to its partition; this field
		/// makes that partition visible to a query and to an operator reading the index.
		/// </summary>
		public string TenantId { get; set; } = string.Empty;
		public string ViewName { get; set; } = string.Empty;
		public long Position { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
		public DateTimeOffset UpdatedAt { get; set; }
	}

	#endregion Internal Document Types
}
