// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Data.DynamoDb.Saga;

/// <summary>
/// DynamoDB implementation of <see cref="ISagaStore"/> using single-table design.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses single-table design with composite keys:
/// <list type="bullet">
/// <item><description>PK: SAGA#{sagaId}</description></item>
/// <item><description>SK: {sagaType}</description></item>
/// </list>
/// </para>
/// <para>
/// Uses read-then-PutItem pattern to preserve createdUtc on updates.
/// </para>
/// </remarks>
public sealed partial class DynamoDbSagaStore : ISagaStore, IAsyncDisposable, IDisposable
{
	private readonly DynamoDbSagaOptions _options;
	private readonly ILogger<DynamoDbSagaStore> _logger;
	private readonly IJsonSerializer _serializer;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly bool _ownsClient;
	private IAmazonDynamoDB? _client;
	private bool _initialized;

	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public DynamoDbSagaStore(
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		IJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	public DynamoDbSagaStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		IJsonSerializer serializer)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_serializer = serializer;
		_initialized = true;
		_ownsClient = false;
	}

	/// <inheritdoc/>
	public async Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var pk = DynamoDbSagaDocument.CreatePK(sagaId);
		var sk = DynamoDbSagaDocument.CreateSK(typeof(TSagaState).Name);

		var request = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSagaDocument.PK] = new() { S = pk },
				[DynamoDbSagaDocument.SK] = new() { S = sk }
			},
			ConsistentRead = _options.UseConsistentReads
		};

		var response = await _client.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return null;
		}

		var stateJson = response.Item[DynamoDbSagaDocument.StateJson].S;
		var result = await _serializer.DeserializeAsync<TSagaState>(stateJson).ConfigureAwait(false);

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var stateJson = _serializer.Serialize(sagaState);
		var sagaType = typeof(TSagaState).Name;
		var pk = DynamoDbSagaDocument.CreatePK(sagaState.SagaId);
		var sk = DynamoDbSagaDocument.CreateSK(sagaType);

		// Read existing to preserve createdUtc
		var getRequest = new GetItemRequest
		{
			TableName = _options.TableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[DynamoDbSagaDocument.PK] = new() { S = pk },
				[DynamoDbSagaDocument.SK] = new() { S = sk }
			},
			ConsistentRead = true
		};

		var existing = await _client.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);

		DateTimeOffset createdUtc;
		if (existing.Item?.Count > 0 && existing.Item.TryGetValue(DynamoDbSagaDocument.CreatedUtc, out var createdAttr))
		{
			createdUtc = DateTimeOffset.Parse(createdAttr.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}
		else
		{
			createdUtc = now;
		}

		var document = DynamoDbSagaDocument.FromSagaState(
			sagaState,
			stateJson,
			createdUtc,
			now,
			_options.DefaultTtlSeconds);

		var putRequest = new PutItemRequest { TableName = _options.TableName, Item = document };

		_ = await _client.PutItemAsync(putRequest, cancellationToken).ConfigureAwait(false);
		LogSagaSaved(sagaType, sagaState.SagaId, sagaState.Completed);
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
					new KeySchemaElement { AttributeName = DynamoDbSagaDocument.PK, KeyType = KeyType.HASH },
					new KeySchemaElement { AttributeName = DynamoDbSagaDocument.SK, KeyType = KeyType.RANGE }
				],
				AttributeDefinitions =
				[
					new AttributeDefinition { AttributeName = DynamoDbSagaDocument.PK, AttributeType = ScalarAttributeType.S },
					new AttributeDefinition { AttributeName = DynamoDbSagaDocument.SK, AttributeType = ScalarAttributeType.S }
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

	[LoggerMessage(DataDynamoDbEventId.SagaStoreInitialized, LogLevel.Information,
		"Initialized DynamoDB saga store with table '{TableName}'")]
	private partial void LogInitialized(string tableName);

	[LoggerMessage(DataDynamoDbEventId.SagaLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataDynamoDbEventId.SagaSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);
}
