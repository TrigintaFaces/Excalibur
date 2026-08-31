// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Configuration options for the CosmosDb CDC state store.
/// </summary>
public sealed class CosmosDbCdcStateStoreOptions
{
	private static readonly CompositeFormat PropertyRequiredFormat =
		CompositeFormat.Parse("{0} is required.");

	/// <summary>
	/// Gets or sets the CosmosDb connection string used to build a client when none is supplied.
	/// </summary>
	/// <remarks>
	/// Required only when the store builds its own client. A host that registers its own
	/// <see cref="CosmosClient"/> — for token-credential authentication, a custom
	/// <c>HttpClientFactory</c>, Gateway mode, or a chosen serializer — supplies the connection
	/// implicitly and may leave this empty.
	/// </remarks>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database identifier.
	/// </summary>
	public string DatabaseId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the container identifier for storing CDC state.
	/// </summary>
	public string ContainerId { get; set; } = "cdc-state";

	/// <summary>
	/// Gets or sets the partition key path for the state container.
	/// </summary>
	public string PartitionKeyPath { get; set; } = "/processorName";

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it doesn't exist.
	/// </summary>
	public bool CreateContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Validates the options for a store that builds its own client from <see cref="ConnectionString"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	public void Validate() => Validate(requireConnectionString: true);

	/// <summary>
	/// Validates the options, optionally waiving <see cref="ConnectionString"/>.
	/// </summary>
	/// <param name="requireConnectionString">
	/// <see langword="false"/> when the caller supplies its own <see cref="CosmosClient"/>, which already
	/// carries the endpoint and credentials the connection string would otherwise provide.
	/// </param>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	internal void Validate(bool requireConnectionString)
	{
		if (requireConnectionString && string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ConnectionString)));
		}

		if (string.IsNullOrWhiteSpace(DatabaseId))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(DatabaseId)));
		}

		if (string.IsNullOrWhiteSpace(ContainerId))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ContainerId)));
		}
	}
}

/// <summary>
/// CosmosDb-based implementation of CDC state store.
/// </summary>
public sealed partial class CosmosDbCdcStateStore : ICosmosDbCdcStateStore
{
	private readonly CosmosClient _client;
	private readonly CosmosDbCdcStateStoreOptions _options;
	private readonly ILogger<CosmosDbCdcStateStore> _logger;

	// A create race, not an ordering problem. Concurrent first-writes for one processor all find no
	// document and all attempt a create, so every writer but the winner is answered 409. Those losers are
	// not stale and there is nothing to order them by -- a continuation token carries no comparable field,
	// so refusing them would discard a write that is every bit as current as the one that won. Retrying
	// converges by construction: once any writer has won, the document exists, so the retried upsert is a
	// replace and cannot conflict again. The bound is margin over a race that resolves on the next attempt,
	// not a backoff schedule; exhausting it rethrows rather than reporting a save that did not happen.
	private const int SaveConflictRetryAttempts = 3;

	// Only a client this store built is a client this store may destroy. A supplied client is a shared
	// singleton the host also hands to its other stores, so disposing it here would tear down connections
	// belonging to code that never asked this store for anything.
	private readonly bool _ownsClient;

	private Container? _container;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcStateStore"/> class, building a client from
	/// <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>.
	/// </summary>
	/// <param name="options">The state store options.</param>
	/// <param name="logger">The logger.</param>
	/// <remarks>
	/// A connection string carries an account key, so this overload cannot authenticate with a
	/// <c>TokenCredential</c>, cannot route through a supplied <c>HttpClientFactory</c>, and cannot select
	/// Gateway mode or a serializer. Use the overload that accepts a <see cref="CosmosClient"/> for any of
	/// those; the store's registration prefers a client the host registered whenever one is present.
	/// </remarks>
	public CosmosDbCdcStateStore(
		IOptions<CosmosDbCdcStateStoreOptions> options,
		ILogger<CosmosDbCdcStateStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;

		_client = new CosmosClient(
			_options.ConnectionString,
			new CosmosClientOptions
			{
				ApplicationName = "CDC-StateStore",
				SerializerOptions = new CosmosSerializationOptions
				{
					PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
				},
			});
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcStateStore"/> class using a client the caller
	/// already configured.
	/// </summary>
	/// <param name="client">
	/// The Cosmos client to read and write state through. The caller keeps ownership: the store never
	/// disposes it.
	/// </param>
	/// <param name="options">The state store options. <see cref="CosmosDbCdcStateStoreOptions.ConnectionString"/>
	/// is not consulted and may be left empty, because <paramref name="client"/> already carries the endpoint
	/// and credentials.</param>
	/// <param name="logger">The logger.</param>
	/// <remarks>
	/// This is the overload that makes token-credential authentication, a custom <c>HttpClientFactory</c>,
	/// Gateway mode, and a chosen serializer reachable — none of which a connection string can express.
	/// </remarks>
	public CosmosDbCdcStateStore(
		CosmosClient client,
		IOptions<CosmosDbCdcStateStoreOptions> options,
		ILogger<CosmosDbCdcStateStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate(requireConnectionString: false);
		_logger = logger;

		_client = client;
		_ownsClient = false;
	}

	/// <inheritdoc/>
	public async Task<CosmosDbCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			var response = await _container!.ReadItemAsync<CdcStateDocument>(
				processorName,
				new PartitionKey(processorName),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (response.Resource?.PositionData is not null)
			{
				if (CosmosDbCdcPosition.TryFromBase64(response.Resource.PositionData, out var position))
				{
					LogPositionLoaded(processorName, position.ToString());
					return position;
				}
			}

			return null;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			LogNoPositionFound(processorName);
			return null;
		}
	}

	/// <inheritdoc/>
	public async Task SavePositionAsync(
		string processorName,
		CosmosDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = new CdcStateDocument
		{
			Id = processorName,
			ProcessorName = processorName,
			PositionData = position.ToBase64(),
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		for (var attempt = 1; ; attempt++)
		{
			try
			{
				_ = await _container!.UpsertItemAsync(
					document,
					new PartitionKey(processorName),
					cancellationToken: cancellationToken).ConfigureAwait(false);

				break;
			}
			catch (CosmosException ex)
				when (ex.StatusCode == HttpStatusCode.Conflict && attempt < SaveConflictRetryAttempts)
			{
				LogPositionSaveRetriedAfterConflict(processorName, attempt);
			}
		}

		LogPositionSaved(processorName, position.ToString());
	}

	/// <inheritdoc/>
	public async Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		_ = await TryDeletePositionAsync(processorName, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deletes the checkpoint for the specified processor and reports whether one was there to delete.
	/// </summary>
	/// <param name="processorName">The processor whose checkpoint to delete.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> when a checkpoint was deleted; <see langword="false"/> when none existed.
	/// </returns>
	/// <remarks>
	/// The absent case is already observable here without a second round trip: Cosmos answers a delete of a
	/// missing document with 404, which is the same response that tells us nothing was removed. Returning it
	/// rather than discarding it is what lets a caller distinguish "reset a live consumer" from "there was
	/// nothing to reset" — a distinction a constant answer cannot express.
	/// </remarks>
	private async Task<bool> TryDeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			_ = await _container!.DeleteItemAsync<CdcStateDocument>(
				processorName,
				new PartitionKey(processorName),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			LogPositionDeleted(processorName);
			return true;
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Already deleted or never existed
			LogPositionNotFoundForDelete(processorName);
			return false;
		}
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not CosmosDbCdcPosition cosmosPosition)
		{
			cosmosPosition = CosmosDbCdcPosition.FromContinuationToken(position.ToToken());
		}

		return SavePositionAsync(consumerId, cosmosPosition, cancellationToken);
	}

	/// <inheritdoc/>
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await TryDeletePositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> ICdcStateStore.GetAllPositionsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var query = _container!.GetItemQueryIterator<CdcStateDocument>();

		while (query.HasMoreResults)
		{
			var response = await query.ReadNextAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in response)
			{
				if (CosmosDbCdcPosition.TryFromBase64(doc.PositionData, out var position))
				{
					yield return (doc.ProcessorName, position);
				}
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
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

		if (_ownsClient)
		{
			_client.Dispose();
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_ownsClient)
		{
			_client.Dispose();
		}
	}

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
			var database = _client.GetDatabase(_options.DatabaseId);

			if (_options.CreateContainerIfNotExists)
			{
				var containerProperties = new ContainerProperties(_options.ContainerId, _options.PartitionKeyPath);

				var response = await database.CreateContainerIfNotExistsAsync(
					containerProperties,
					cancellationToken: cancellationToken).ConfigureAwait(false);

				_container = response.Container;
			}
			else
			{
				_container = database.GetContainer(_options.ContainerId);
			}

			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	// Source-generated logging
	[LoggerMessage(DataCosmosDbEventId.CdcPositionLoaded, LogLevel.Debug, "Loaded position for processor '{ProcessorName}': {Position}")]
	private partial void LogPositionLoaded(string processorName, string position);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionNotFound, LogLevel.Debug, "No position found for processor '{ProcessorName}'")]
	private partial void LogNoPositionFound(string processorName);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionSaved, LogLevel.Debug, "Saved position for processor '{ProcessorName}': {Position}")]
	private partial void LogPositionSaved(string processorName, string position);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionDeleted, LogLevel.Debug, "Deleted position for processor '{ProcessorName}'")]
	private partial void LogPositionDeleted(string processorName);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionSaveRetriedAfterConflict, LogLevel.Debug, "Retrying position save for processor '{ProcessorName}' after a create conflict (attempt {Attempt})")]
	private partial void LogPositionSaveRetriedAfterConflict(string processorName, int attempt);

	[LoggerMessage(DataCosmosDbEventId.CdcPositionNotFoundForDeletion, LogLevel.Debug, "Position not found for deletion for processor '{ProcessorName}'")]
	private partial void LogPositionNotFoundForDelete(string processorName);

	/// <summary>
	/// Internal document structure for storing CDC state in CosmosDb.
	/// </summary>
	/// <remarks>
	/// Every property carries BOTH a <see cref="JsonPropertyNameAttribute"/> (System.Text.Json) and a
	/// <see cref="Newtonsoft.Json.JsonPropertyAttribute"/> with the same lowercase name, so the document serializes
	/// to the same keys whichever serializer the client in use happens to have. This became load-bearing the
	/// moment the store accepted a caller-supplied client: the store's own client forces camelCase naming, but
	/// a supplied one need not, and the Cosmos SDK v3 default serializer is Newtonsoft with no naming policy.
	/// Under that default an unannotated document emits PascalCase <c>"Id"</c>/<c>"ProcessorName"</c>, which
	/// leaves Cosmos's required system key <c>id</c> absent and the document's partition-key field mismatched
	/// against the <c>/processorName</c> path — so the point read never finds the checkpoint and the processor
	/// silently resumes from the beginning on every restart. The names chosen here are exactly what the
	/// camelCase policy already produced, so documents written before this annotation still read back.
	/// </remarks>
	private sealed class CdcStateDocument
	{
		/// <summary>
		/// Gets or sets the document ID (matches processor name; Cosmos-required lowercase <c>id</c>).
		/// </summary>
		[JsonPropertyName("id")]
		[Newtonsoft.Json.JsonProperty("id")]
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the processor name (partition key, path <c>/processorName</c>).
		/// </summary>
		[JsonPropertyName("processorName")]
		[Newtonsoft.Json.JsonProperty("processorName")]
		public string ProcessorName { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the serialized position data (base64).
		/// </summary>
		[JsonPropertyName("positionData")]
		[Newtonsoft.Json.JsonProperty("positionData")]
		public string PositionData { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets when this state was last updated.
		/// </summary>
		[JsonPropertyName("updatedAt")]
		[Newtonsoft.Json.JsonProperty("updatedAt")]
		public DateTimeOffset UpdatedAt { get; set; }
	}
}
