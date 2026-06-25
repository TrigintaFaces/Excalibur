// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IL2026 // JSON serialization fallback path uses reflection when consumer does not provide source-gen JsonSerializerOptions

namespace Excalibur.Data.DynamoDb.Projections;

/// <summary>
/// DynamoDB implementation of <see cref="IProjectionStore{TProjection}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores projections as JSON-serialized items in a DynamoDB table.
/// Uses a single-table design with projection type as a key prefix.
/// Supports auto-table creation and dictionary-based filter queries via scan.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type to store.</typeparam>
public sealed class DynamoDbProjectionStore<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] TProjection>
	: IProjectionStore<TProjection>, ICursorProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Root-level key for the framework metadata object. All framework-managed fields
	/// (id, type, updatedAt) are nested under this key to prevent collisions with
	/// consumer projection properties. MongoDB and CosmosDB stores use the same key.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Nesting metadata as a DynamoDB Map attribute adds a small per-item storage overhead
	/// compared to flat root-level attributes. This is an intentional trade-off: the
	/// collision-safety guarantee outweighs the marginal increase in write cost units
	/// (typically &lt;1 WCU for the metadata map).
	/// </para>
	/// </remarks>
	private const string MetadataKey = "_projection";

	/// <summary>Metadata field: the original projection ID passed to UpsertAsync.</summary>
	private const string MetaFieldId = "id";

	/// <summary>Metadata field: the projection type discriminator for scan filtering.</summary>
	private const string MetaFieldType = "type";

	/// <summary>Metadata field: UTC timestamp of the last upsert.</summary>
	private const string MetaFieldUpdatedAt = "updatedAt";

	/// <summary>
	/// Metadata field: preserves the projection's original partition key value if the
	/// consumer projection type has a property whose camelCase name matches the configured
	/// <see cref="DynamoDbProjectionStoreOptions.PartitionKeyName"/>. Without this, the
	/// compound PK (<c>{type}#{id}</c>) would overwrite the projection's own value.
	/// Restored during deserialization by <see cref="DeserializeItem"/>.
	/// </summary>
	private const string MetaFieldOrigPk = "origPk";

	private readonly IAmazonDynamoDB _client;
	private readonly DynamoDbProjectionStoreOptions _options;
	private readonly ILogger<DynamoDbProjectionStore<TProjection>> _logger;
	private readonly string _projectionType;
	private readonly JsonSerializerOptions _jsonOptions;
	private volatile bool _tableVerified;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbProjectionStore{TProjection}"/> class.
	/// </summary>
	/// <param name="client">The DynamoDB client.</param>
	/// <param name="options">The projection store options.</param>
	/// <param name="logger">The logger instance.</param>
	public DynamoDbProjectionStore(
		IAmazonDynamoDB client,
		IOptions<DynamoDbProjectionStoreOptions> options,
		ILogger<DynamoDbProjectionStore<TProjection>> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_logger = logger;
		_projectionType = typeof(TProjection).Name;
		_jsonOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		var response = await _client.GetItemAsync(new GetItemRequest
		{
			TableName = _options.TableName,
			Key = CreateKey(id),
		}, cancellationToken).ConfigureAwait(false);

		if (response.HttpStatusCode != HttpStatusCode.OK || !response.IsItemSet)
		{
			return null;
		}

		return DeserializeItem(response.Item);
	}

	/// <summary>
	/// Deserializes a DynamoDB item (flat attributes) back to a projection instance.
	/// Metadata (PK, _projection) is removed before deserialization so framework
	/// fields don't interfere with the projection type's properties. If the partition
	/// key name collided with a projection property, the original value is restored
	/// from <c>_projection.origPk</c>.
	/// </summary>
	private TProjection? DeserializeItem(Dictionary<string, AttributeValue> item)
	{
		// Convert low-level attributes to Document model
		var doc = Document.FromAttributeMap(item);

		// Restore original PK value if it was preserved during write (collision case)
		if (doc.TryGetValue(MetadataKey, out var metaEntry) && metaEntry is Document metaDoc)
		{
			if (metaDoc.TryGetValue(MetaFieldOrigPk, out var origPk))
			{
				// Replace compound PK with the projection's original property value
				doc[_options.PartitionKeyName] = origPk;
			}
			else
			{
				// No collision — remove the compound PK entirely
				doc.Remove(_options.PartitionKeyName);
			}
		}
		else
		{
			doc.Remove(_options.PartitionKeyName);
		}

		doc.Remove(MetadataKey);

		var json = doc.ToJson();

		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Serialize projection to JSON, then convert to flat DynamoDB attributes.
		// Projection properties live at the item root. Framework metadata is isolated
		// under a nested '_projection' map to prevent field name collisions.
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var doc = Document.FromJson(json);

		// Framework metadata — nested under '_projection' to avoid collisions
		var metadata = new Document();
		metadata[MetaFieldId] = id;
		metadata[MetaFieldType] = _projectionType;
		metadata[MetaFieldUpdatedAt] = DateTimeOffset.UtcNow.ToString("O");

		// Preserve the projection's original PK-named property if one exists (collision case).
		// The partition key name is configurable, so if a consumer's projection happens to
		// have a property whose camelCase name matches (e.g., "pk"), we must save its value.
		if (doc.TryGetValue(_options.PartitionKeyName, out var origPk))
		{
			metadata[MetaFieldOrigPk] = origPk;
		}

		// Partition key must be at root level (DynamoDB requirement) — overwrites any
		// existing projection property with the same name (restored by DeserializeItem).
		doc[_options.PartitionKeyName] = $"{_projectionType}#{id}";
		doc[MetadataKey] = metadata;

		var item = doc.ToAttributeMap();

		await _client.PutItemAsync(new PutItemRequest
		{
			TableName = _options.TableName,
			Item = item,
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		await _client.DeleteItemAsync(new DeleteItemRequest
		{
			TableName = _options.TableName,
			Key = CreateKey(id),
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Scan with the projection-type discriminator AND every supplied filter as a predicate
		// (FR-P1.1/FR-P1.3). A null/empty filter leaves only the discriminator (AC-P1.4).
		var (filterExpression, names, values) = BuildScanFilter(filters);

		var request = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = filterExpression,
			ExpressionAttributeNames = names,
			ExpressionAttributeValues = values,
		};

		if (options?.Take > 0)
		{
			request.Limit = options.Take.Value;
		}

		var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

		var results = new List<TProjection>();
		foreach (var item in response.Items)
		{
			var projection = DeserializeItem(item);
			if (projection != null)
			{
				results.Add(projection);
			}
		}

		return results;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Count only the projections matching the supplied filter (FR-P1.2).
		var (filterExpression, names, values) = BuildScanFilter(filters);
		return await ComputeTotalAsync(filterExpression, names, values, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<CursorPagedResult<TProjection>> QueryCursorAsync(
		IDictionary<string, object>? filters,
		string? cursor,
		int pageSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Apply the type discriminator AND any supplied filters as the scan predicate (same translation
		// as QueryAsync/CountAsync).
		var (filterExpression, names, values) = BuildScanFilter(filters);

		// The cursor carries both the partition-key continuation point AND the total record count. The
		// total is computed ONCE (on the first page) and carried forward, so continuation pages never
		// re-issue a COUNT scan — the per-page full COUNT scan the previous implementation ran is gone
		// (FR-P2.1 / AC-P2.3), and the count is never the 1 MB-truncated partial (FR-P2.2 / AC-P2.2).
		var (startKey, carriedTotal) = DecodeCursor(cursor);

		// DynamoDB Scan `Limit` caps the number of items SCANNED, not MATCHED — it is applied BEFORE the
		// FilterExpression. A single scan with Limit=pageSize can therefore return fewer than pageSize
		// matched items while a continuation key is still set. To return a full page of MATCHED items
		// (FR-P2.3 / AC-P2.1) we scan repeatedly following LastEvaluatedKey, with Limit set to exactly the
		// number still needed — so each call consumes everything it scans up to its LastEvaluatedKey (no
		// skip/duplicate across pages, EC-P2.3) and never matches more than the page needs (no overshoot).
		var results = new List<TProjection>(pageSize);
		Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

		while (results.Count < pageSize)
		{
			var request = new ScanRequest
			{
				TableName = _options.TableName,
				FilterExpression = filterExpression,
				ExpressionAttributeNames = names,
				ExpressionAttributeValues = values,
				Limit = pageSize - results.Count,
				ExclusiveStartKey = startKey,
			};

			var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				var projection = DeserializeItem(item);
				if (projection is not null)
				{
					results.Add(projection);
				}
			}

			lastEvaluatedKey = response.LastEvaluatedKey is { Count: > 0 } lek ? lek : null;
			startKey = lastEvaluatedKey;

			if (lastEvaluatedKey is null)
			{
				// Table exhausted before the page filled — this is the final page (AC-P2.4).
				break;
			}
		}

		// Reuse the carried total on continuation pages; compute it once on the first page by following
		// the COUNT continuation to exhaustion so it is the true total, never a 1 MB-truncated partial.
		var totalRecords = carriedTotal
			?? await ComputeTotalAsync(filterExpression, names, values, cancellationToken).ConfigureAwait(false);

		// Surface a continuation cursor only when the page filled AND DynamoDB signalled more to scan.
		// When the table is exhausted (lastEvaluatedKey is null) the cursor is null, so the walk ends
		// cleanly with no phantom next page (AC-P2.4).
		string? nextCursor = null;
		if (results.Count >= pageSize
			&& lastEvaluatedKey is { Count: > 0 } finalKey
			&& finalKey.TryGetValue(_options.PartitionKeyName, out var lastPk))
		{
			nextCursor = CursorEncoder.Encode(lastPk.S, totalRecords);
		}

		return new CursorPagedResult<TProjection>(results, pageSize, totalRecords, nextCursor);
	}

	/// <summary>
	/// Counts the projections matching the predicate, following the COUNT scan's <c>LastEvaluatedKey</c>
	/// to exhaustion so the result is the true total rather than a 1 MB-truncated partial (FR-P2.2).
	/// </summary>
	private async Task<long> ComputeTotalAsync(
		string filterExpression,
		Dictionary<string, string> names,
		Dictionary<string, AttributeValue> values,
		CancellationToken cancellationToken)
	{
		long total = 0;
		Dictionary<string, AttributeValue>? startKey = null;

		do
		{
			var request = new ScanRequest
			{
				TableName = _options.TableName,
				FilterExpression = filterExpression,
				ExpressionAttributeNames = names,
				ExpressionAttributeValues = values,
				Select = Select.COUNT,
				ExclusiveStartKey = startKey,
			};

			var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);
			total += response.Count ?? 0;
			startKey = response.LastEvaluatedKey is { Count: > 0 } lastKey ? lastKey : null;
		}
		while (startKey is not null);

		return total;
	}

	private (Dictionary<string, AttributeValue>? StartKey, long? Total) DecodeCursor(string? cursor)
	{
		if (string.IsNullOrEmpty(cursor))
		{
			return (null, null);
		}

		// Cursor encodes [partition-key value, total]. The projection table uses a single HASH key, so
		// the partition-key value fully reconstructs the ExclusiveStartKey; the total is carried forward
		// from the first page to avoid recomputing it per page.
		var cursorValues = CursorEncoder.Decode(cursor);
		if (cursorValues is not { Length: > 0 } || cursorValues[0] is not string pkValue)
		{
			return (null, null);
		}

		var startKey = new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyName] = new() { S = pkValue },
		};

		var total = cursorValues.Length > 1 && cursorValues[1] is long carried ? carried : (long?)null;
		return (startKey, total);
	}

	/// <summary>
	/// Builds the DynamoDB <c>FilterExpression</c> combining the projection-type discriminator
	/// (<c>#proj.#type = :projType</c>) with every supplied filter, AND-combined (FR-P1.3, EC-P1.1).
	/// Filter attribute names use a dedicated <c>#f{n}</c> prefix and values a <c>:v{n}</c> prefix, so
	/// they never collide with the discriminator placeholders even when a filter key matches the
	/// metadata field name (EC-P1.3). A null/empty filter yields the discriminator alone (AC-P1.4).
	/// </summary>
	private (string FilterExpression, Dictionary<string, string> Names, Dictionary<string, AttributeValue> Values) BuildScanFilter(
		IDictionary<string, object>? filters)
	{
		var names = new Dictionary<string, string>
		{
			["#proj"] = MetadataKey,
			["#type"] = MetaFieldType,
		};

		var values = new Dictionary<string, AttributeValue>
		{
			[":projType"] = new() { S = _projectionType },
		};

		if (filters is null || filters.Count == 0)
		{
			return ("#proj.#type = :projType", names, values);
		}

		var conditions = new List<string>(filters.Count);
		var nameIndex = 0;
		var valueIndex = 0;

		foreach (var (key, value) in filters)
		{
			var parsed = FilterParser.Parse(key);

			// Projection properties are stored as flat root attributes using the camelCase JSON naming
			// policy, so map the filter property name to its stored (camelCase) attribute name.
			var attributeName = ToCamelCase(parsed.PropertyName);
			var namePlaceholder = $"#f{nameIndex++}";
			names[namePlaceholder] = attributeName;

			conditions.Add(BuildFilterCondition(parsed.Operator, namePlaceholder, value, values, ref valueIndex));
		}

		var filterExpression = $"#proj.#type = :projType AND {string.Join(" AND ", conditions)}";
		return (filterExpression, names, values);
	}

	private static string BuildFilterCondition(
		FilterOperator op,
		string namePlaceholder,
		object? value,
		Dictionary<string, AttributeValue> values,
		ref int valueIndex)
	{
		return op switch
		{
			FilterOperator.Equals => $"{namePlaceholder} = {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.NotEquals => $"{namePlaceholder} <> {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.GreaterThan => $"{namePlaceholder} > {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.GreaterThanOrEqual => $"{namePlaceholder} >= {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.LessThan => $"{namePlaceholder} < {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.LessThanOrEqual => $"{namePlaceholder} <= {AddFilterValue(value, values, ref valueIndex)}",
			FilterOperator.Contains => $"contains({namePlaceholder}, {AddFilterValue(value, values, ref valueIndex)})",
			FilterOperator.In => BuildInCondition(namePlaceholder, value, values, ref valueIndex),
			_ => $"{namePlaceholder} = {AddFilterValue(value, values, ref valueIndex)}",
		};
	}

	private static string BuildInCondition(
		string namePlaceholder,
		object? value,
		Dictionary<string, AttributeValue> values,
		ref int valueIndex)
	{
		// A non-enumerable (or string) value degrades to an equality check.
		if (value is not IEnumerable enumerable || value is string)
		{
			return $"{namePlaceholder} = {AddFilterValue(value, values, ref valueIndex)}";
		}

		var placeholders = new List<string>();
		foreach (var item in enumerable)
		{
			placeholders.Add(AddFilterValue(item, values, ref valueIndex));
		}

		// An empty IN list matches nothing. Emit an always-false predicate (the discriminator can never
		// be both equal and not-equal to the projection type) rather than invalid empty-IN syntax.
		return placeholders.Count == 0
			? "#proj.#type <> :projType"
			: $"{namePlaceholder} IN ({string.Join(", ", placeholders)})";
	}

	private static string AddFilterValue(object? value, Dictionary<string, AttributeValue> values, ref int valueIndex)
	{
		var placeholder = $":v{valueIndex++}";
		values[placeholder] = ToAttributeValue(value);
		return placeholder;
	}

	/// <summary>
	/// Converts a filter value to the DynamoDB <see cref="AttributeValue"/> type matching how the
	/// projection property was stored (string → S, bool → BOOL, number → N) (EC-P1.2). An untranslatable
	/// value type throws <see cref="NotSupportedException"/> rather than silently dropping the predicate
	/// (FR-P1.5).
	/// </summary>
	private static AttributeValue ToAttributeValue(object? value)
	{
		return value switch
		{
			// A null filter value is ambiguous (NULL-typed attribute vs. absent attribute) and cannot be
			// translated to an unambiguous equality predicate — signal "can't honor the contract" (FR-P1.5)
			// rather than silently issuing an unfiltered or mis-filtered scan.
			null => throw new NotSupportedException(
				"DynamoDB projection filter cannot translate a null filter value. Provide a concrete value, "
				+ "or omit the key to leave the attribute unfiltered."),
			string s => new AttributeValue { S = s },
			bool b => new AttributeValue { BOOL = b },
			byte or sbyte or short or ushort or int or uint or long or ulong
				=> new AttributeValue { N = Convert.ToString(value, CultureInfo.InvariantCulture)! },
			float f => new AttributeValue { N = f.ToString(CultureInfo.InvariantCulture) },
			double d => new AttributeValue { N = d.ToString(CultureInfo.InvariantCulture) },
			decimal m => new AttributeValue { N = m.ToString(CultureInfo.InvariantCulture) },
			Guid g => new AttributeValue { S = g.ToString() },
			DateTimeOffset dto => new AttributeValue { S = dto.ToString("O", CultureInfo.InvariantCulture) },
			DateTime dt => new AttributeValue { S = dt.ToString("O", CultureInfo.InvariantCulture) },
			_ => throw new NotSupportedException(
				$"DynamoDB projection filter cannot translate a filter value of type '{value.GetType()}'. " +
				"Supported types: string, bool, integral and floating-point numbers, Guid, DateTime, DateTimeOffset."),
		};
	}

	private static string ToCamelCase(string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
		{
			return propertyName;
		}

		return $"{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";
	}

	private Dictionary<string, AttributeValue> CreateKey(string id)
	{
		return new Dictionary<string, AttributeValue>
		{
			[_options.PartitionKeyName] = new() { S = $"{_projectionType}#{id}" },
		};
	}

	private async Task EnsureTableAsync(CancellationToken cancellationToken)
	{
		if (_tableVerified || !_options.AutoCreateTable)
		{
			return;
		}

		try
		{
			await _client.DescribeTableAsync(_options.TableName, cancellationToken).ConfigureAwait(false);
		}
		catch (ResourceNotFoundException)
		{
			await _client.CreateTableAsync(new CreateTableRequest
			{
				TableName = _options.TableName,
				KeySchema =
				[
					new KeySchemaElement(_options.PartitionKeyName, KeyType.HASH),
				],
				AttributeDefinitions =
				[
					new AttributeDefinition(_options.PartitionKeyName, ScalarAttributeType.S),
				],
				BillingMode = BillingMode.PAY_PER_REQUEST,
			}, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Created DynamoDB projection table {TableName}", _options.TableName);
		}

		_tableVerified = true;
	}
}
