// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL2046, IL3050, IL3051 // AOT: Cloud-native provider uses reflection-based serialization

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Excalibur.Data;
using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Saga.CosmosDb;

/// <summary>
/// Cosmos DB implementation of <see cref="ISagaStore"/> for managing saga state persistence.
/// </summary>
/// <remarks>
/// <para>
/// Provides durable storage for saga state using Cosmos DB document storage.
/// Uses read-then-upsert pattern to preserve the original creation timestamp
/// while updating other fields on subsequent saves.
/// </para>
/// <para>
/// This class supports two constructor patterns:
/// <list type="bullet">
/// <item><description>Simple: Options-based configuration for most users</description></item>
/// <item><description>Advanced: Existing CosmosClient for shared client instances</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class CosmosDbSagaStore : ISagaStore, IAsyncDisposable, IDisposable
{
	private readonly CosmosDbSagaOptions _options;
	private readonly ILogger<CosmosDbSagaStore> _logger;
	private readonly DispatchJsonSerializer _serializer;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private CosmosClient? _client;
	private Container? _container;
	private volatile bool _initialized;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// This is the primary constructor for dependency injection scenarios.
	/// </remarks>
	public CosmosDbSagaStore(
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing Cosmos DB client.</param>
	/// <param name="options">The saga store options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <remarks>
	/// <para>
	/// This is the advanced constructor for scenarios that need custom connection management:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Shared client instances across multiple stores</description></item>
	/// <item><description>Custom connection configuration</description></item>
	/// <item><description>Integration with existing Cosmos DB infrastructure</description></item>
	/// </list>
	/// </remarks>
	public CosmosDbSagaStore(
		CosmosClient client,
		IOptions<CosmosDbSagaOptions> options,
		ILogger<CosmosDbSagaStore> logger,
		DispatchJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = CosmosDbSagaDocument.CreateId(sagaId);
		var sagaType = typeof(TSagaState).Name;

		try
		{
			var response = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
				documentId,
				new PartitionKey(sagaType),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var document = response.Resource;
			if (string.IsNullOrEmpty(document.StateJson))
			{
				return null;
			}

			var result = _serializer.Deserialize<TSagaState>(document.StateJson);
			if (result is not null)
			{
				// The authoritative optimistic-concurrency version is the dedicated document field, NOT the
				// version embedded in StateJson (serialized before the store-owns-increment write-back, so it
				// carries the stale pre-save version). Apply it so load-modify-save gates against the real value.
				result.Version = document.Version;
			}

			LogSagaLoaded(sagaType, sagaId);
			return result;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stateJson = _serializer.Serialize(sagaState);
		var now = DateTimeOffset.UtcNow;
		var sagaType = typeof(TSagaState).Name;
		var documentId = CosmosDbSagaDocument.CreateId(sagaState.SagaId);
		var partitionKey = new PartitionKey(sagaType);
		var expectedVersion = sagaState.Version;

		// Optimistic concurrency (bd-e1tsq2), mirroring SqlServerSagaStore's version-gated MERGE: the
		// persisted version must equal the loaded (expected) version, otherwise a ConcurrencyException is
		// thrown instead of silently overwriting a newer write (the previous unconditional upsert lost
		// concurrent updates). IfMatchEtag closes the read->write race window: a writer that commits between
		// our read and our upsert changes the document's _etag, so the upsert fails with 412 PreconditionFailed.
		try
		{
			// Try to read existing saga to obtain the current version + etag and preserve createdUtc.
			var readResponse = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
				documentId,
				partitionKey,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var existing = readResponse.Resource;

			if (existing.Version != expectedVersion)
			{
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					existing.Version);
			}

			// Update document preserving createdUtc, advancing the version.
			var document = new CosmosDbSagaDocument
			{
				Id = documentId,
				SagaId = sagaState.SagaId,
				SagaType = sagaType,
				StateJson = stateJson,
				IsCompleted = sagaState.Completed,
				Version = expectedVersion + 1,
				CreatedUtc = existing.CreatedUtc, // Preserve original
				UpdatedUtc = now
			};

			try
			{
				_ = await _container!.UpsertItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions
					{
						EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
						IfMatchEtag = readResponse.ETag,
					},
					cancellationToken).ConfigureAwait(false);
			}
			catch (CosmosException upsertEx) when (upsertEx.StatusCode == HttpStatusCode.PreconditionFailed)
			{
				// A concurrent writer modified the document between our read and our write (etag mismatch).
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					actualVersion: -1L);
			}

			LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// No-resurrect guard (SqlServer reference contract): only a brand-new saga (expected version 0)
			// may be created. A stale save (expected > 0) against a missing document is a deleted/completed
			// saga — throw rather than resurrect it at a high version (zombie saga). Mirrors the MERGE's
			// "@ExpectedVersion = 0"-guarded INSERT branch.
			if (expectedVersion != 0)
			{
				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					actualVersion: -1L);
			}

			// No existing saga, create new with current timestamp as createdUtc.
			var document = new CosmosDbSagaDocument
			{
				Id = documentId,
				SagaId = sagaState.SagaId,
				SagaType = sagaType,
				StateJson = stateJson,
				IsCompleted = sagaState.Completed,
				Version = expectedVersion + 1,
				CreatedUtc = now,
				UpdatedUtc = now
			};

			try
			{
				_ = await _container!.CreateItemAsync(
					document,
					partitionKey,
					new ItemRequestOptions { EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite },
					cancellationToken).ConfigureAwait(false);

				LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
			}
			catch (CosmosException createEx) when (createEx.StatusCode == HttpStatusCode.Conflict)
			{
				// Race: another process created the document between our NotFound read and our create. That
				// is a concurrency conflict — surface it instead of clobbering the other writer's state.
				var conflictReadResponse = await _container!.ReadItemAsync<CosmosDbSagaDocument>(
					documentId,
					partitionKey,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				throw new ConcurrencyException(
					nameof(SagaState),
					sagaState.SagaId.ToString(),
					expectedVersion,
					conflictReadResponse.Resource.Version);
			}
		}

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			if (_client == null)
			{
				var clientOptions = CreateClientOptions();
				_client = CreateClient(clientOptions);
			}

			var database = _client.GetDatabase(_options.DatabaseName);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath);

				if (_options.DefaultTtlSeconds != 0)
				{
					containerProperties.DefaultTimeToLive = _options.DefaultTtlSeconds;
				}

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					_options.ContainerThroughput,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.ContainerName);
			}

			_initialized = true;
			LogInitialized(_options.ContainerName);
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	private CosmosClientOptions CreateClientOptions()
	{
		var options = new CosmosClientOptions
		{
			MaxRetryAttemptsOnRateLimitedRequests = _options.Client.Resilience.MaxRetryAttempts,
			MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(_options.Client.Resilience.MaxRetryWaitTimeInSeconds),
			EnableContentResponseOnWrite = _options.Client.Resilience.EnableContentResponseOnWrite,
			RequestTimeout = TimeSpan.FromSeconds(_options.Client.Resilience.RequestTimeoutInSeconds),
			ConnectionMode = _options.Client.UseDirectMode ? ConnectionMode.Direct : ConnectionMode.Gateway,
			UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
			{
				PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
			}
		};

		if (_options.Client.ConsistencyLevel.HasValue)
		{
			options.ConsistencyLevel = _options.Client.ConsistencyLevel.Value;
		}

		if (_options.Client.PreferredRegions is { Count: > 0 })
		{
			options.ApplicationPreferredRegions = _options.Client.PreferredRegions.ToList();
		}

		if (_options.Client.HttpClientFactory != null)
		{
			options.HttpClientFactory = _options.Client.HttpClientFactory;
		}

		return options;
	}

	private CosmosClient CreateClient(CosmosClientOptions options)
	{
		if (!string.IsNullOrWhiteSpace(_options.Client.ConnectionString))
		{
			return new CosmosClient(_options.Client.ConnectionString, options);
		}

		return new CosmosClient(_options.Client.AccountEndpoint, _options.Client.AccountKey, options);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock.Dispose();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_client?.Dispose();
		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	[LoggerMessage(DataCosmosDbEventId.SagaStoreInitialized, LogLevel.Information,
		"Initialized Cosmos DB saga store with container '{ContainerName}'")]
	private partial void LogInitialized(string containerName);

	[LoggerMessage(DataCosmosDbEventId.SagaStateLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataCosmosDbEventId.SagaStateSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);
}
