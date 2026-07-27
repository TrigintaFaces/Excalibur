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
using Excalibur.Dispatch;
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

	private readonly ITenantContext? _tenantContext;
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
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public DynamoDbSagaStore(
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		_tenantContext = tenantContext;
		_ownsClient = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSagaStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. It is accepted so the
	/// store can DETECT a tenant scope it cannot honour and refuse it, rather than silently ignoring one.
	/// </param>
	public DynamoDbSagaStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext? tenantContext = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_serializer = serializer;
		_tenantContext = tenantContext;
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

		// A saga owned by another tenant is "not found" from this scope. GetItem cannot carry a predicate, so
		// the item crosses the wire before it is discarded — weaker than a filtered query, and the strongest
		// control available on a key-addressed read.
		if (!OwnedByCurrentScope(response.Item))
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

		// Ownership is assigned at creation and CARRIED OVER on update, never recomputed from the ambient
		// scope on an existing item — recomputing would let a save under a different scope re-home a saga.
		var scope = TenantScope.FromContext(_tenantContext);
		var owner = existing.Item?.Count > 0
			&& existing.Item.TryGetValue(DynamoDbSagaDocument.TenantId, out var ownerAttr)
				? ownerAttr.S
				: scope.IsScoped ? scope.TenantId : sagaState.TenantId;

		if (!string.IsNullOrEmpty(owner))
		{
			document[DynamoDbSagaDocument.TenantId] = new AttributeValue { S = owner };
		}

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
			// The tenant is part of the CONDITION, not merely of the item. DynamoDB evaluates this server-side,
			// so a save carrying another tenant's key is REJECTED by the database rather than discarded by us —
			// strictly stronger than the read-side check above, and the reason a cross-tenant OVERWRITE is
			// impossible here even though a cross-tenant READ has to be filtered client-side.
			putRequest.ConditionExpression = scope.IsScoped
				? "#v = :expectedVersion AND #t = :tenantId"
				: "#v = :expectedVersion AND attribute_not_exists(#t)";
			putRequest.ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#v"] = DynamoDbSagaDocument.Version,
				["#t"] = DynamoDbSagaDocument.TenantId
			};
			putRequest.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":expectedVersion"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
			
			};
			if (scope.IsScoped)
			{
				putRequest.ExpressionAttributeValues[":tenantId"] = new AttributeValue { S = scope.TenantId };
			}
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

	/// <inheritdoc/>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		// This store has no tenant discriminator: it persists the saga state as a serialized blob, so the
		// tenant travels INSIDE the document rather than as a queryable field. It cannot build a server-side
		// tenant predicate, which makes it an untenanted-only store -- a coherent, supported shape under the
		// settled semantics, where every row it owns lives in the untenanted partition.
		//
		// So an unscoped purge is correct and proceeds. A SCOPED purge is refused rather than serviced,
		// because the only thing this store could do with a tenant is ignore it -- and ignoring it here means
		// deleting every OTHER tenant's completed sagas while reporting success. This is a range delete with
		// no reachability gate: unlike a point load, the caller needs nothing but a timestamp to destroy
		// another tenant's data. Failing loud is the one honest answer available to it.
		var scope = TenantScope.FromContext(_tenantContext);
		if (scope.IsScoped)
		{
			throw new TenantScopeNotSupportedException(
				$"This saga store cannot purge within a tenant scope. Store type: '{GetType().FullName}'. " +
				"It persists saga state as a serialized document, so the tenant is not a queryable field and " +
				"no tenant predicate can be applied. Servicing the call would delete every tenant's completed " +
				"sagas. Use a store that discriminates by tenant (SQL Server, Postgres, Oracle), or call " +
				"PurgeAllTenantsCompletedBeforeAsync if an estate-wide sweep is what you intended.");
		}

		return PurgeAllTenantsCompletedBeforeAsync(threshold, cancellationToken);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep, and the only purge this store can perform. It is identical to the unscoped
	/// path above because a store with no tenant discriminator cannot distinguish the two — which is exactly
	/// why the scoped call refuses instead of silently landing here.
	/// </remarks>
	public async Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// DynamoDB has no native "delete where" — Scan with a FilterExpression, then batch-delete the matches.
		// attribute_exists(#c) excludes running sagas (which have no completedAt), so only completed sagas older
		// than the threshold are removed. The completedAt attribute is a round-trip UTC "O" string, so a string
		// comparison against the same-encoded cutoff is a valid chronological range.
		var cutoff = threshold.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
		var removed = 0;
		Dictionary<string, AttributeValue>? lastKey = null;

		do
		{
			cancellationToken.ThrowIfCancellationRequested();

			var scan = new ScanRequest
			{
				TableName = _options.TableName,
				FilterExpression = "attribute_exists(#c) AND #c < :cutoff",
				ExpressionAttributeNames = new Dictionary<string, string> { ["#c"] = DynamoDbSagaDocument.CompletedAt },
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":cutoff"] = new() { S = cutoff } },
				ProjectionExpression = "#pk, #sk",
				ExclusiveStartKey = lastKey,

				// Eventual consistency is sufficient for a periodic retention sweep (a just-completed saga can
				// wait for the next cycle) and halves the RCU cost vs a strongly-consistent scan.
				ConsistentRead = false,
			};
			scan.ExpressionAttributeNames["#pk"] = DynamoDbSagaDocument.PK;
			scan.ExpressionAttributeNames["#sk"] = DynamoDbSagaDocument.SK;

			var response = await _client!.ScanAsync(scan, cancellationToken).ConfigureAwait(false);

			// Delete matched items in batches of 25 (BatchWriteItem's per-request cap).
			for (var i = 0; i < response.Items.Count; i += 25)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var chunk = response.Items.GetRange(i, Math.Min(25, response.Items.Count - i));
				var writes = chunk.Select(item => new WriteRequest
				{
					DeleteRequest = new DeleteRequest
					{
						Key = new Dictionary<string, AttributeValue>
						{
							[DynamoDbSagaDocument.PK] = item[DynamoDbSagaDocument.PK],
							[DynamoDbSagaDocument.SK] = item[DynamoDbSagaDocument.SK],
						},
					},
				}).ToList();

				var batch = new Dictionary<string, List<WriteRequest>> { [_options.TableName] = writes };

				// Drain UnprocessedItems (throttling) so every matched item is actually deleted before it counts.
				do
				{
					cancellationToken.ThrowIfCancellationRequested();
					var batchResponse = await _client!
						.BatchWriteItemAsync(new BatchWriteItemRequest { RequestItems = batch }, cancellationToken)
						.ConfigureAwait(false);
					batch = batchResponse.UnprocessedItems is { Count: > 0 } ? batchResponse.UnprocessedItems : [];
				} while (batch.Count > 0);

				removed += chunk.Count;
			}

			lastKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
		} while (lastKey is not null);

		LogSagasPurged(removed, threshold);
		return removed;
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

			try
			{
				_ = await _client!.CreateTableAsync(createRequest, cancellationToken).ConfigureAwait(false);
			}
			catch (ResourceInUseException)
			{
				// Multi-instance cold-start race: another instance created (or is creating) the table
				// between our DescribeTable and CreateTable. Benign — fall through to wait-for-active.
			}

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

	/// <summary>
	/// Returns whether an item belongs to the store's current scope.
	/// </summary>
	/// <remarks>
	/// A DynamoDB <c>GetItem</c> addresses an item by primary key; there is no predicate to attach, so a read's
	/// ownership is established after the fetch. WRITES are different and stronger — <c>PutItem</c> accepts a
	/// ConditionExpression, so the tenant is enforced server-side there and a cross-tenant write cannot land
	/// even if this check were bypassed.
	/// </remarks>
	private bool OwnedByCurrentScope(Dictionary<string, AttributeValue> item)
	{
		var scope = TenantScope.FromContext(_tenantContext);
		var owner = item.TryGetValue(DynamoDbSagaDocument.TenantId, out var attr) ? attr.S : null;
		return string.Equals(owner, scope.IsScoped ? scope.TenantId : null, StringComparison.Ordinal);
	}

	[LoggerMessage(DataDynamoDbEventId.SagaStoreInitialized, LogLevel.Information,
		"Initialized DynamoDB saga store with table '{TableName}'")]
	private partial void LogInitialized(string tableName);

	[LoggerMessage(DataDynamoDbEventId.SagaLoaded, LogLevel.Debug, "Loaded saga {SagaType}/{SagaId}")]
	private partial void LogSagaLoaded(string sagaType, Guid sagaId);

	[LoggerMessage(DataDynamoDbEventId.SagaSaved, LogLevel.Debug, "Saved saga {SagaType}/{SagaId}, Completed={IsCompleted}")]
	private partial void LogSagaSaved(string sagaType, Guid sagaId, bool isCompleted);

	[LoggerMessage(DataDynamoDbEventId.SagasPurged, LogLevel.Debug, "Purged {PurgedCount} completed sagas older than {Threshold}")]
	private partial void LogSagasPurged(int purgedCount, DateTimeOffset threshold);
}
