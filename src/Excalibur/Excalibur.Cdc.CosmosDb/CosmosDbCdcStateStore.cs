// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

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
	/// Gets or sets the CosmosDb connection string.
	/// </summary>
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
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
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

	private Container? _container;
	// Serialises first-time initialisation. Without it concurrent first callers each run the
	// provisioning below, and where more than one field is assigned a second caller can observe
	// a partly-built state and dereference null. Same defect class as the MongoDB stores.
	private readonly SemaphoreSlim _initLock = new(1, 1);

	// volatile: read on the fast path outside the lock.
	private volatile bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbCdcStateStore"/> class.
	/// </summary>
	/// <param name="options">The state store options.</param>
	/// <param name="logger">The logger.</param>
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

		_ = await _container!.UpsertItemAsync(
			document,
			new PartitionKey(processorName),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		LogPositionSaved(processorName, position.ToString());
	}

	/// <inheritdoc/>
	public async Task DeletePositionAsync(
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
		}
		catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
		{
			// Already deleted or never existed
			LogPositionNotFoundForDelete(processorName);
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
	async Task<bool> ICdcStateStore.DeletePositionAsync(string consumerId, CancellationToken cancellationToken)
	{
		await DeletePositionAsync(consumerId, cancellationToken).ConfigureAwait(false);
		return true;
	}

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
		_client.Dispose();
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
		_client.Dispose();
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

	[LoggerMessage(DataCosmosDbEventId.CdcPositionNotFoundForDeletion, LogLevel.Debug, "Position not found for deletion for processor '{ProcessorName}'")]
	private partial void LogPositionNotFoundForDelete(string processorName);

	/// <summary>
	/// Internal document structure for storing CDC state in CosmosDb.
	/// </summary>
	private sealed class CdcStateDocument
	{
		/// <summary>
		/// Gets or sets the document ID (matches processor name).
		/// </summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the processor name (partition key).
		/// </summary>
		public string ProcessorName { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the serialized position data (base64).
		/// </summary>
		public string PositionData { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets when this state was last updated.
		/// </summary>
		public DateTimeOffset UpdatedAt { get; set; }
	}
}
