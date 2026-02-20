// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Snapshots;

/// <summary>
/// DynamoDB implementation of <see cref="ISnapshotStore"/> using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses single-table design with composite keys:
/// <list type="bullet">
/// <item><description>PK: SNAPSHOT#{aggregateId}</description></item>
/// <item><description>SK: {aggregateType}</description></item>
/// </list>
/// </para>
/// <para>
/// Version ordering is enforced using conditional PutItem expressions to ensure
/// older snapshots never overwrite newer ones during concurrent operations.
/// </para>
/// </remarks>
public sealed partial class DynamoDbSnapshotStore : ISnapshotStore, IAsyncDisposable, IDisposable
{
	private readonly DynamoDbSnapshotStoreOptions _options;
	private readonly ILogger<DynamoDbSnapshotStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly bool _ownsClient;
	private IAmazonDynamoDB? _client;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSnapshotStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbSnapshotStore(
		IOptions<DynamoDbSnapshotStoreOptions> options,
		ILogger<DynamoDbSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSnapshotStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbSnapshotStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbSnapshotStoreOptions> options,
		ILogger<DynamoDbSnapshotStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_initialized = true;
		_ownsClient = false;
	}

	/// <summary>
	/// Initializes the DynamoDB client.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async ValueTask InitializeAsync(CancellationToken cancellationToken)
	{
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

			_client = CreateClient();

			if (_options.CreateTableIfNotExists)
			{
				await EnsureTableExistsAsync(cancellationToken).ConfigureAwait(false);
			}

			_initialized = true;
			LogInitialized(_options.TableName);
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = DynamoDbSnapshotDocument.CreatePK(aggregateId);
		var sk = DynamoDbSnapshotDocument.CreateSK(aggregateType);

		var request = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSnapshotDocument.PK] = new() { S = pk },
				[DynamoDbSnapshotDocument.SK] = new() { S = sk }
			},
			ConsistentRead = _options.UseConsistentReads
		};

		try
		{
			var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

			if (response.Item == null || response.Item.Count == 0)
			{
				result = WriteStoreTelemetry.Results.NotFound;
				return null;
			}

			var snapshot = DynamoDbSnapshotDocument.ToSnapshot(response.Item);
			LogSnapshotRetrieved(aggregateType, aggregateId, snapshot.Version);

			return snapshot;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"load",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(snapshot);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = DynamoDbSnapshotDocument.CreatePK(snapshot.AggregateId);
		var sk = DynamoDbSnapshotDocument.CreateSK(snapshot.AggregateType);

		// First, check if a snapshot already exists to perform version check
		var getRequest = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSnapshotDocument.PK] = new() { S = pk },
				[DynamoDbSnapshotDocument.SK] = new() { S = sk }
			},
			ConsistentRead = true
		};

		try
		{
			var existing = await _client.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);

			if (existing.Item?.Count > 0)
			{
				var existingVersion = long.Parse(
					existing.Item[DynamoDbSnapshotDocument.Version].N,
					CultureInfo.InvariantCulture);

				if (existingVersion >= snapshot.Version)
				{
					// Older or same version - skip silently
					result = WriteStoreTelemetry.Results.Conflict;
					LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
					return;
				}
			}

			// Put with conditional expression - version must still be lower OR not exist
			var document = DynamoDbSnapshotDocument.FromSnapshot(snapshot, _options.DefaultTtlSeconds);

			var putRequest = new PutItemRequest
			{
				TableName = _options.TableName,
				Item = document,
				ConditionExpression = "attribute_not_exists(PK) OR #version < :newVersion",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = DynamoDbSnapshotDocument.Version },
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":newVersion"] = new() { N = snapshot.Version.ToString(CultureInfo.InvariantCulture) }
				}
			};

			try
			{
				_ = await _client.PutItemAsync(putRequest, cancellationToken).ConfigureAwait(false);
				LogSnapshotSaved(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
			catch (ConditionalCheckFailedException)
			{
				// Race condition - another process wrote a newer version between our read and write
				result = WriteStoreTelemetry.Results.Conflict;
				LogSnapshotVersionSkipped(snapshot.AggregateType, snapshot.AggregateId, snapshot.Version);
			}
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"save",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = DynamoDbSnapshotDocument.CreatePK(aggregateId);
		var sk = DynamoDbSnapshotDocument.CreateSK(aggregateType);

		var request = new DeleteItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSnapshotDocument.PK] = new() { S = pk },
				[DynamoDbSnapshotDocument.SK] = new() { S = sk }
			}
		};

		try
		{
			_ = await _client.DeleteItemAsync(request, cancellationToken).ConfigureAwait(false);
			LogSnapshotDeleted(aggregateType, aggregateId);
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"delete",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = DynamoDbSnapshotDocument.CreatePK(aggregateId);
		var sk = DynamoDbSnapshotDocument.CreateSK(aggregateType);

		// First read the snapshot to check its version
		var getRequest = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSnapshotDocument.PK] = new() { S = pk },
				[DynamoDbSnapshotDocument.SK] = new() { S = sk }
			},
			ConsistentRead = _options.UseConsistentReads
		};

		try
		{
			var response = await _client.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);

			if (response.Item == null || response.Item.Count == 0)
			{
				// No snapshot exists
				result = WriteStoreTelemetry.Results.NotFound;
				return;
			}

			var existingVersion = long.Parse(
				response.Item[DynamoDbSnapshotDocument.Version].N,
				CultureInfo.InvariantCulture);

			// Only delete if version is less than olderThanVersion
			if (existingVersion >= olderThanVersion)
			{
				// Snapshot is not older than the specified version
				return;
			}

			// Delete with conditional expression to handle race conditions
			var deleteRequest = new DeleteItemRequest
			{
				TableName = _options.TableName,
				Key = new Dictionary<string, AttributeValue>
				{
					[DynamoDbSnapshotDocument.PK] = new() { S = pk },
					[DynamoDbSnapshotDocument.SK] = new() { S = sk }
				},
				ConditionExpression = "#version < :olderThanVersion",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#version"] = DynamoDbSnapshotDocument.Version },
				ExpressionAttributeValues = new Dictionary<string, AttributeValue>
				{
					[":olderThanVersion"] = new() { N = olderThanVersion.ToString(CultureInfo.InvariantCulture) }
				}
			};

			try
			{
				_ = await _client.DeleteItemAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
				LogSnapshotOlderDeleted(aggregateType, aggregateId, olderThanVersion);
			}
			catch (ConditionalCheckFailedException)
			{
				result = WriteStoreTelemetry.Results.Conflict;
				// Race condition - the snapshot was modified or a newer version now exists
				// In this case, we don't delete since a newer snapshot should be kept
			}
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.SnapshotStore,
				WriteStoreTelemetry.Providers.DynamoDb,
				"delete_older_than",
				result,
				stopwatch.Elapsed);
		}
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
			_client?.Dispose();
		}

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

		if (_ownsClient)
		{
			_client?.Dispose();
		}

		_initLock.Dispose();

		await ValueTask.CompletedTask.ConfigureAwait(false);
	}

	private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
	{
		try
		{
			_ = await _client.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			var createRequest = new CreateTableRequest
			{
				TableName = _options.TableName,
				KeySchema =
				[
					new KeySchemaElement { AttributeName = DynamoDbSnapshotDocument.PK, KeyType = KeyType.HASH },
					new KeySchemaElement { AttributeName = DynamoDbSnapshotDocument.SK, KeyType = KeyType.RANGE }
				],
				AttributeDefinitions =
				[
					new AttributeDefinition { AttributeName = DynamoDbSnapshotDocument.PK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbSnapshotDocument.SK, AttributeType = ScalarAttributeType.S }
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			};

			_ = await _client.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);

			// Wait for table to be active
			var describeRequest = new DescribeTableRequest { TableName = _options.TableName };
			TableStatus status;
			do
			{
				await Task.Delay(500, cancellationToken).ConfigureAwait(false);
				var describeResponse = await _client.DescribeTableAsync(describeRequest, cancellationToken)
					.ConfigureAwait(false);
				status = describeResponse.Table.TableStatus;
			} while (status != TableStatus.ACTIVE);

			// Enable TTL if configured
			if (_options.DefaultTtlSeconds > 0)
			{
				var ttlRequest = new UpdateTimeToLiveRequest
				{
					TableName = _options.TableName,
					TimeToLiveSpecification = new TimeToLiveSpecification { Enabled = true, AttributeName = _options.TtlAttributeName }
				};

				_ = await _client.UpdateTimeToLiveAsync(ttlRequest, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private IAmazonDynamoDB CreateClient()
	{
		var config = new AmazonDynamoDBConfig
		{
			Timeout = TimeSpan.FromSeconds(_options.TimeoutInSeconds),
			MaxErrorRetry = _options.MaxRetryAttempts
		};

		if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
		{
			config.ServiceURL = _options.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.AccessKey) && !string.IsNullOrWhiteSpace(_options.SecretKey))
		{
			var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
			return new AmazonDynamoDBClient(credentials, config);
		}

		return new AmazonDynamoDBClient(config);
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	[LoggerMessage(DataDynamoDbEventId.SnapshotStoreInitialized, LogLevel.Information,
		"Initialized DynamoDB snapshot store with table '{TableName}'")]
	private partial void LogInitialized(string tableName);

	[LoggerMessage(DataDynamoDbEventId.SnapshotSaved, LogLevel.Debug,
		"Saved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotSaved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataDynamoDbEventId.SnapshotSkipped, LogLevel.Debug,
		"Skipped saving older snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotVersionSkipped(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataDynamoDbEventId.SnapshotRetrieved, LogLevel.Debug,
		"Retrieved snapshot for {AggregateType}/{AggregateId} at version {Version}")]
	private partial void LogSnapshotRetrieved(string aggregateType, string aggregateId, long version);

	[LoggerMessage(DataDynamoDbEventId.SnapshotDeleted, LogLevel.Debug, "Deleted snapshot for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotDeleted(string aggregateType, string aggregateId);

	[LoggerMessage(DataDynamoDbEventId.SnapshotDeletedOlderThan, LogLevel.Debug,
		"Deleted snapshot older than version {Version} for {AggregateType}/{AggregateId}")]
	private partial void LogSnapshotOlderDeleted(string aggregateType, string aggregateId, long version);
}
