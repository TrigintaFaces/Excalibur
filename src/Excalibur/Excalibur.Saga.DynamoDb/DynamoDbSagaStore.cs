// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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

using Microsoft.Extensions.DependencyInjection;
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

	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	private readonly SemaphoreSlim _initLock = new(1, 1);
	private readonly bool _ownsClient;
	private IAmazonDynamoDB? _client;
	private volatile bool _initialized;

	private volatile bool _disposed;

	// Set only once the legacy-item probe has come back clean. Separate from _initialized because the probe
	// is deliberately NOT on the initialisation path: it runs at the first point the store would act on the
	// ABSENCE of an item, which is the first moment an unaddressable saga could be mistaken for one that was
	// never started.
	private volatile bool _legacyItemsProbed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbSagaStore"/> class.
	/// </summary>
	/// <param name="options">The configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="serializer">The JSON serializer for saga state serialization.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	// Deterministic DI construction: the advanced constructor below also accepts an ITenantContext, so
	// without this marker ActivatorUtilities' selection depends on which services happen to be
	// registered, and reports a missing dependency as a constructor ambiguity.
	[ActivatorUtilitiesConstructor]
	public DynamoDbSagaStore(
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
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
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public DynamoDbSagaStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbSagaOptions> options,
		ILogger<DynamoDbSagaStore> logger,
		DispatchJsonSerializer serializer,
		ITenantContext tenantContext)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(serializer);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_serializer = serializer;
		ArgumentNullException.ThrowIfNull(tenantContext);
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

		// The tenant is part of the item's IDENTITY, so this scope addresses its own item rather than a shared
		// one it must then be refused. The ownership check below is retained on top: it is redundant for an
		// item this store wrote (identity and stored attribute are assigned once from the same scope, and the
		// attribute is never re-stamped) and is the check that still holds for one it did not.
		var pk = DynamoDbSagaDocument.CreatePK(CurrentTenantScope.TenantId, sagaId);
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
			// The ABSENCE decision, and the one the caller acts on: a null here is read as "no saga in
			// flight", so the caller starts the saga over and re-fires every compensating action and external
			// call it already performed. An item written under the pre-tenant partition key answers exactly
			// this way, because the keyed read cannot address it.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);
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
			// The version attribute is authoritative for concurrency, independent of any Version
			// embedded in the JSON blob. The store uses it as the compare-and-swap basis on the next save.
			result.Version = version;
		}

		LogSagaLoaded(typeof(TSagaState).Name, sagaId);
		return result;
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("The saga state is serialized with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "ISagaStore is implemented by stores that never reach reflective serialization, so the requirement cannot be declared on the interface without binding those too. It is declared on this cloud store's SaveAsync instead.")]
	public async Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(sagaState);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var stateJson = _serializer.Serialize(sagaState);
		var sagaType = typeof(TSagaState).Name;
		var pk = DynamoDbSagaDocument.CreatePK(CurrentTenantScope.TenantId, sagaState.SagaId);
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

		// Optimistic-concurrency compare-and-swap, store-owns-increment (mirrors SqlServerSagaStore's
		// TWO guarded MERGE branches). SagaState.Version is the version the caller LOADED (the concurrency token;
		// a brand-new saga is 0) -- the caller performs NO version arithmetic. The conditional PutItem is the
		// atomic CAS.
		//
		// SA ruling: the insert leg is guarded to expected == 0 so a deleted/completed saga cannot be
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

		// Ownership is assigned at creation and CARRIED OVER on update, never recomputed from the ambient
		// scope on an existing item — recomputing would let a save under a different scope re-home a saga.
		var scope = CurrentTenantScope;

		// The partition key is built from the CURRENT scope, deliberately, and not from `owner` below. The two
		// are the same value for every item this store wrote, because a scope can only ever address a key it
		// composed itself; deriving the key from the item's stored attribute instead would mean reading an
		// item to discover where to write it, which is the shared key space this change removes.
		var document = DynamoDbSagaDocument.FromSagaState(
			sagaState,
			stateJson,
			newVersion,
			createdUtc,
			now,
			scope.TenantId,
			_options.DefaultTtlSeconds);

		var owner = existing.Item?.Count > 0
			&& existing.Item.TryGetValue(DynamoDbSagaDocument.TenantId, out var ownerAttr)
				? ownerAttr.S
				: scope.TenantId;

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
			// The conditional create acts on absence too, and more destructively than the load: the
			// attribute_not_exists guard is evaluated against the NEW partition key, so a saga already running
			// under the old one does not fail the condition - it is simply invisible, and the guard SUCCEEDS,
			// creating a second, duplicate saga beside the original. Probed before the write, while nothing
			// has been modified.
			await EnsureEmptyReadIsTrustworthyAsync(cancellationToken).ConfigureAwait(false);

			putRequest.ConditionExpression = "attribute_not_exists(#pk)";
			putRequest.ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#pk"] = DynamoDbSagaDocument.PK
			};
		}
		else
		{
			// The tenant is in the CONDITION as well as in the key. The key is what makes a cross-tenant
			// overwrite unaddressable; the condition is what the database itself still evaluates server-side for
			// an item this store did not write. Retained rather than dropped as now-redundant: it costs one
			// attribute comparison and it is the only server-side term left if a key were ever composed
			// elsewhere.
			putRequest.ConditionExpression = "#v = :expectedVersion AND #t = :tenantId";
			putRequest.ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#v"] = DynamoDbSagaDocument.Version,
				["#t"] = DynamoDbSagaDocument.TenantId
			};
			putRequest.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":expectedVersion"] = new() { N = expectedVersion.ToString(CultureInfo.InvariantCulture) },
			
			};
			putRequest.ExpressionAttributeValues[":tenantId"] = new AttributeValue { S = scope.TenantId };
		}

		try
		{
			_ = await _client!.PutItemAsync(putRequest, cancellationToken).ConfigureAwait(false);
		}
		catch (ConditionalCheckFailedException)
		{
			// A concurrent handler advanced this saga between our load and save: surface it as a
			// ConcurrencyException instead of silently overwriting the winner (the previous unconditional
			// PutItem was last-writer-wins and lost that update).
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
	/// <remarks>
	/// The tenant IS a discriminator on this item: it is stored as its own top-level attribute beside the
	/// state blob, not inside it, so the scan below applies it as a real filter term rather than refusing on
	/// the grounds that it cannot. <see cref="TenantScope.TenantId"/> is total -- untenanted, the single-tenant
	/// default, and a real tenant all bind a concrete term -- so this purge always filters, never refuses.
	/// </remarks>
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, CurrentTenantScope.TenantId, cancellationToken);

	/// <inheritdoc/>
	/// <remarks>
	/// The estate-wide sweep: no tenant predicate, every tenant's completed sagas in range. Reachable only by
	/// calling this method directly, never as a fallback from the scoped purge above.
	/// </remarks>
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeCoreAsync(threshold, tenantId: null, cancellationToken);

	/// <summary>
	/// Scans and deletes completed, aged saga items, optionally confined to one tenant.
	/// </summary>
	/// <param name="threshold">Sagas completed strictly before this instant are eligible.</param>
	/// <param name="tenantId">
	/// The tenant term to filter on, or <see langword="null"/> for the estate-wide sweep that applies no
	/// tenant predicate at all.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task<int> PurgeCoreAsync(DateTimeOffset threshold, string? tenantId, CancellationToken cancellationToken)
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

		var filterExpression = "attribute_exists(#c) AND #c < :cutoff";
		var expressionAttributeNames = new Dictionary<string, string> { ["#c"] = DynamoDbSagaDocument.CompletedAt };
		var expressionAttributeValues = new Dictionary<string, AttributeValue> { [":cutoff"] = new() { S = cutoff } };
		if (tenantId is not null)
		{
			filterExpression += " AND #t = :tenantId";
			expressionAttributeNames["#t"] = DynamoDbSagaDocument.TenantId;
			expressionAttributeValues[":tenantId"] = new AttributeValue { S = tenantId };
		}

		do
		{
			cancellationToken.ThrowIfCancellationRequested();

			var scan = new ScanRequest
			{
				TableName = _options.TableName,
				FilterExpression = filterExpression,
				ExpressionAttributeNames = new Dictionary<string, string>(expressionAttributeNames),
				ExpressionAttributeValues = expressionAttributeValues,
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

	/// <summary>
	/// Verifies, at most once per store instance, that an absent saga is genuinely absent rather than merely
	/// unaddressable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called from every point at which this store is about to act on the ABSENCE of an item, and from
	/// nowhere else. A read that returns an item proves the table is addressable and needs no probe; only
	/// silence is ambiguous, and only silence is checked.
	/// </para>
	/// <para>
	/// Deliberately not on the initialisation path, and here that matters more than on any other provider:
	/// the probe is a filtered <c>Scan</c>, so running it at initialisation would spend a scan page on every
	/// process start - on every serverless cold start, forever - to detect a condition that can only hold
	/// across a one-time upgrade. Here it costs nothing at startup, nothing on a read that finds an item, and
	/// at most one scan page per store instance.
	/// </para>
	/// <para>
	/// Unsynchronised: two concurrent first-absence decisions may both probe. The probe reads and modifies
	/// nothing, so a duplicate costs one extra request and nothing else - cheaper than serialising every
	/// empty read behind a lock. The flag is set only once the probe has come back clean, so a table that
	/// holds legacy items refuses every call rather than only the first.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	private async Task EnsureEmptyReadIsTrustworthyAsync(CancellationToken cancellationToken)
	{
		if (_legacyItemsProbed)
		{
			return;
		}

		await RefuseLegacyUntenantedItemsAsync(cancellationToken).ConfigureAwait(false);
		_legacyItemsProbed = true;
	}

	/// <summary>
	/// Refuses when the saga table still holds an item written under the untenanted partition-key shape of an
	/// earlier release. Called only through <see cref="EnsureEmptyReadIsTrustworthyAsync"/>, which decides
	/// when it runs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Such an item is unaddressable under the current key shape, and the failure that follows is silent: a
	/// load returns NO SAGA rather than an error, so the caller treats a saga that is already part-executed as
	/// new and starts it again - re-firing every compensating action and every external call that has already
	/// happened. On the create path the same silence lets the <c>attribute_not_exists</c> guard succeed and a
	/// second, duplicate saga be written beside the original. Refusing converts that silence into a failure
	/// while both the state and the correlation are still intact.
	/// </para>
	/// <para>
	/// Nothing is modified. Which tenant owns an existing untenanted item is a question about the deployment
	/// rather than about the data, so it cannot be decided here; the message states the procedure instead.
	/// </para>
	/// <para>
	/// The filter is two-sided rather than a single negated prefix, because this is a single-table design and
	/// the tenant segment is not the leading one: the first term selects saga items, and only then does the
	/// second reject those whose key carries no tenant. Testing the tenant segment alone would match no key
	/// at all - every saga key begins with the item-kind discriminator - so a one-sided probe would report
	/// every table clean, or, negated, every table dirty.
	/// </para>
	/// <para>
	/// The partition key is the only place the tenant appears, and DynamoDB has no ordered access across
	/// partitions, so this is one filtered <c>Scan</c> request rather than an index range read. It reads a
	/// single page: a table upgraded in place carries the old shape on EVERY saga item, so the first page
	/// cannot miss it, and bounding the request keeps a large correctly-keyed table from paying for a full
	/// scan. A table that holds both shapes only beyond the first page - which takes a partial rollback to
	/// produce - is not detected here.
	/// </para>
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="InvalidOperationException">
	/// The table holds at least one saga item whose partition key carries no tenant segment.
	/// </exception>
	private async Task RefuseLegacyUntenantedItemsAsync(CancellationToken cancellationToken)
	{
		ScanResponse response;

		try
		{
			response = await _client!.ScanAsync(
				new ScanRequest
				{
					TableName = _options.TableName,
					ProjectionExpression = "#pk",
					FilterExpression = "begins_with(#pk, :sagaPrefix) AND NOT begins_with(#pk, :tenantedPrefix)",
					ExpressionAttributeNames = new Dictionary<string, string>
					{
						["#pk"] = DynamoDbSagaDocument.PK
					},
					ExpressionAttributeValues = new Dictionary<string, AttributeValue>
					{
						[":sagaPrefix"] = new() { S = DynamoDbSagaDocument.SagaPrefix },
						[":tenantedPrefix"] = new() { S = DynamoDbSagaDocument.TenantedPartitionKeyPrefix }
					}
				},
				cancellationToken).ConfigureAwait(false);
		}
		catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
		{
			// The table has not been provisioned, so it holds nothing to refuse. A read against a missing
			// table still fails on its own path, with the error that path already produces.
			return;
		}

		var legacyItem = response.Items?.FirstOrDefault();

		if (legacyItem is null)
		{
			return;
		}

		var legacyKey = legacyItem.TryGetValue(DynamoDbSagaDocument.PK, out var partitionKey)
			? partitionKey.S
			: "(unreadable)";

		throw new InvalidOperationException(
			$"Saga table '{_options.TableName}' holds at least one saga item whose partition key " +
			$"('{legacyKey}') carries no tenant segment, so it was written by a release that stored sagas " +
			$"without one. Those items are unaddressable under the current key shape: a load of the saga they " +
			$"belong to reports no saga in flight, so the caller starts it again and re-runs every " +
			$"compensating action and external call it has already performed, and a create writes a second " +
			$"saga beside the first. Nothing has been modified. Stop the saga host, export every saga item, " +
			$"re-key each one so its partition key reads " +
			$"'{DynamoDbSagaDocument.TenantedPartitionKeyPrefix}<tenantId>:<sagaId>' with the tenant that " +
			$"owns the saga, re-import, and start the application again.");
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

		_initLock?.Dispose();
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

		_initLock?.Dispose();

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
		var scope = CurrentTenantScope;
		var owner = item.TryGetValue(DynamoDbSagaDocument.TenantId, out var attr) ? attr.S : null;
		return string.Equals(owner, scope.TenantId, StringComparison.Ordinal);
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
