// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL2046, IL3050, IL3051 // AOT: Cloud-native provider uses reflection-based serialization

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.Data;
using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Saga.DynamoDb;

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
	private readonly DispatchJsonSerializer _serializer;
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
		DispatchJsonSerializer serializer)
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
		DispatchJsonSerializer serializer)
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

		var response = await _client!.GetItemAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.Item == null || response.Item.Count == 0)
		{
			return null;
		}

		var stateJson = response.Item[DynamoDbSagaDocument.StateJson].S;
		var result = _serializer.Deserialize<TSagaState>(stateJson);

		if (result is not null
			&& response.Item.TryGetValue(DynamoDbSagaDocument.Version, out var versionAttr)
			&& long.TryParse(versionAttr.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
		{
			// The version attribute is authoritative for concurrency (skl8r7), independent of any Version
			// embedded in the JSON blob. The store uses it as the compare-and-swap basis on the next save.
			result.Version = version;
		}

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

		var existing = await _client!.GetItemAsync(getRequest, cancellationToken).ConfigureAwait(false);

		DateTimeOffset createdUtc;
		if (existing.Item?.Count > 0 && existing.Item.TryGetValue(DynamoDbSagaDocument.CreatedUtc, out var createdAttr))
		{
			createdUtc = DateTimeOffset.Parse(createdAttr.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}
		else
		{
			createdUtc = now;
		}

		// Optimistic-concurrency compare-and-swap (skl8r7), store-owns-increment (mirrors SqlServerSagaStore's
		// TWO guarded MERGE branches). SagaState.Version is the version the caller LOADED (the concurrency token;
		// a brand-new saga is 0) -- the caller performs NO version arithmetic. The conditional PutItem is the
		// atomic CAS.
		//
		// SA ruling (skl8r7): the insert leg is guarded to expected == 0 so a deleted/completed saga cannot be
		// RESURRECTED at a high version (a "zombie" saga). Branching the ConditionExpression by the expected
		// version is the canonical DynamoDB form (and avoids a value-to-value literal comparison):
		//   - expected == 0 (new saga) -> "attribute_not_exists(#pk)": the put succeeds only if no item exists;
		//     a pre-existing row fails the condition (a fresh-insert collision IS a conflict).
		//   - expected  > 0 (update)   -> "#v = :expectedVersion": the put succeeds only if the persisted version
		//     still equals the expected one. A missing item (deleted/zombie) has no #v attribute, so the
		//     comparison is false and the put is REJECTED -> no resurrection. A stale version is likewise
		//     rejected. ("version" is a DynamoDB reserved word, referenced via the #v name placeholder.)
		// Either rejection raises ConditionalCheckFailedException, surfaced below as a ConcurrencyException.
		var expectedVersion = sagaState.Version;
		var newVersion = expectedVersion + 1;

		var document = DynamoDbSagaDocument.FromSagaState(
			sagaState,
			stateJson,
			newVersion,
			createdUtc,
			now,
			_options.DefaultTtlSeconds);

		var putRequest = new PutItemRequest
		{
			TableName = _options.TableName,
			Item = document
		};

		if (expectedVersion == 0)
		{
			putRequest.ConditionExpression = "attribute_not_exists(#pk)";
			putRequest.ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#pk"] = DynamoDbSagaDocument.PK
			};
		}
		else
		{
			putRequest.ConditionExpression = "#v = :expectedVersion";
			putRequest.ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#v"] = DynamoDbSagaDocument.Version
			};
			putRequest.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":expectedVersion"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) }
			};
		}

		try
		{
			_ = await _client!.PutItemAsync(putRequest, cancellationToken).ConfigureAwait(false);
		}
		catch (ConditionalCheckFailedException)
		{
			// A concurrent handler advanced this saga between our load and save: surface it as a
			// ConcurrencyException instead of silently overwriting the winner (the previous unconditional
			// PutItem was last-writer-wins and lost that update, skl8r7).
			var current = await LoadAsync<TSagaState>(sagaState.SagaId, cancellationToken).ConfigureAwait(false);

			throw new ConcurrencyException(
				nameof(SagaState),
				sagaState.SagaId.ToString(),
				expectedVersion,
				current?.Version ?? -1L);
		}

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = newVersion;

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
			_ = await _client!.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
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

			_ = await _client!.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);

			// Wait for table to be active
			var describeRequest = new DescribeTableRequest { TableName = _options.TableName };
			TableStatus status;
			do
			{
				await Task.Delay(500, cancellationToken).ConfigureAwait(false);
				var describeResponse = await _client!.DescribeTableAsync(describeRequest, cancellationToken)
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

				_ = await _client!.UpdateTimeToLiveAsync(ttlRequest, cancellationToken).ConfigureAwait(false);
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

		if (!string.IsNullOrWhiteSpace(_options.Connection.ServiceUrl))
		{
			config.ServiceURL = _options.Connection.ServiceUrl;
		}
		else if (_options.GetRegionEndpoint() is { } region)
		{
			config.RegionEndpoint = region;
		}

		if (!string.IsNullOrWhiteSpace(_options.Connection.AccessKey) && !string.IsNullOrWhiteSpace(_options.Connection.SecretKey))
		{
			var credentials = new BasicAWSCredentials(_options.Connection.AccessKey, _options.Connection.SecretKey);
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
