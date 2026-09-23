// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

#pragma warning disable IL2026, IL3050 // JSON serialization fallback path uses reflection when consumer does not provide source-gen JsonSerializerOptions

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
		_jsonOptions = _options.JsonSerializerOptions ?? ProjectionSerializationDefaults.CreateReadModelOptions();
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresDynamicCode("Deserializes the projection type with the reflection-based System.Text.Json serializer, which generates converters at run time.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	/// <remarks>
	/// <para>
	/// <b>Supplying <see cref="QueryOptions.OrderBy"/> makes this a FULL READ of every matched projection.</b>
	/// DynamoDB's <c>Scan</c> has no server-side sort, so the rows must all be retrieved before any of them
	/// can be known to be first. The store therefore reads every match before applying
	/// <see cref="QueryOptions.Skip"/> and <see cref="QueryOptions.Take"/>.
	/// </para>
	/// <para>
	/// The consequence is that <b><see cref="QueryOptions.Take"/> does not bound the cost of an ordered
	/// query</b> — it bounds only the rows returned. Bound the cost with the <c>filters</c> argument
	/// instead, so that fewer rows match. An ordered query over an unfiltered table reads the table.
	/// </para>
	/// <para>
	/// Ordering is not refused, because refusing it would make this the one projection store where a
	/// portable query stops working. It is not applied per page either: sorting a page bounded by
	/// <see cref="QueryOptions.Take"/> would order an arbitrary subset and return rows that are not the
	/// first by the requested key, and nothing in the result would distinguish that from a correct answer.
	/// </para>
	/// <para>
	/// For bounded paging that does not read the whole match set, use the cursor API this store also
	/// implements (<see cref="ICursorProjectionStore{TProjection}"/>); it pages without ordering.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Scan with the projection-type discriminator AND every supplied filter as a predicate
		//. A null/empty filter leaves only the discriminator.
		var (filterExpression, names, values) = BuildScanFilter(filters);

		// Follow LastEvaluatedKey to exhaustion. A single Scan reads a bounded amount of the table, so one
		// request returns only the projections that happened to fall in it -- and nothing in the returned
		// list distinguishes that from the complete set. Take caps MATCHED projections; Scan's own Limit
		// caps items SCANNED (it is applied before the filter), so it can only bound the work per request
		// and never fill the result. The loop is what fills it -- the same shape QueryPagedAsync uses.
		var take = options?.Take > 0 ? options.Take.Value : (int?)null;
		var skip = options?.Skip > 0 ? options.Skip.Value : 0;
		var orderBy = string.IsNullOrWhiteSpace(options?.OrderBy) ? null : options.OrderBy;

		// ORDERING FORCES A FULL DRAIN, and that is a correctness requirement rather than a tuning choice.
		// DynamoDB Scan has no server-side sort -- it yields items in whatever order the partitions produce
		// them -- so a page truncated by Take BEFORE sorting would order the wrong subset and return rows
		// that are not the first by the requested key, with nothing to distinguish the result from a correct
		// one. When OrderBy is set the scan therefore sees every match before Skip and Take are applied, and
		// the cost is bounded by the number of MATCHED projections rather than by Take: narrow the filter if
		// that set is large. Without OrderBy the per-request bound below still applies.
		var mustDrain = orderBy is not null;

		var results = new List<TProjection>();
		var ordered = mustDrain ? new List<Dictionary<string, AttributeValue>>() : null;
		var skipped = 0;
		Dictionary<string, AttributeValue>? startKey = null;

		do
		{
			var request = new ScanRequest
			{
				TableName = _options.TableName,
				FilterExpression = filterExpression,
				ExpressionAttributeNames = names,
				ExpressionAttributeValues = values,
				ExclusiveStartKey = startKey,
			};

			// Bounding the request is only sound when nothing is pending that could reorder the result, and
			// the bound must cover the skipped prefix as well as the page itself.
			if (!mustDrain && take is { } remaining)
			{
				request.Limit = skip + remaining - results.Count;
			}

			var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);

			foreach (var item in response.Items)
			{
				if (ordered is not null)
				{
					ordered.Add(item);
					continue;
				}

				var projection = DeserializeItem(item);
				if (projection is null)
				{
					continue;
				}

				// Skip counts MATCHED projections, not scanned items, so an item the filter admitted but the
				// deserializer rejected must not consume a place in the skipped prefix.
				if (skipped < skip)
				{
					skipped++;
					continue;
				}

				results.Add(projection);

				if (take is { } cap && results.Count >= cap)
				{
					return results;
				}
			}

			startKey = response.LastEvaluatedKey is { Count: > 0 } lastEvaluatedKey ? lastEvaluatedKey : null;
		}
		while (startKey is not null);

		if (ordered is null)
		{
			return results;
		}

		// Resolve the caller's property name to the attribute actually stored, ONCE. Projections are
		// written through a camelCase naming policy, so a caller ordering by the documented CLR property
		// name ("Rank") is asking for an attribute stored as "rank". Matching only exactly would find
		// nothing and return the set unordered -- silently, which is the defect this method is fixing.
		var sortKey = ResolveAttributeName(ordered, orderBy!);

		ordered.Sort((left, right) => CompareByAttribute(left, right, sortKey, options!.Descending));

		for (var index = skip; index < ordered.Count; index++)
		{
			var projection = DeserializeItem(ordered[index]);
			if (projection is null)
			{
				continue;
			}

			results.Add(projection);

			if (take is { } cap && results.Count >= cap)
			{
				break;
			}
		}

		return results;
	}

	/// <summary>
	/// Maps the caller's order-by name onto the attribute key the items actually carry.
	/// </summary>
	/// <remarks>
	/// An exact match always wins. Only when nothing matches exactly is a case-insensitive match used,
	/// which is what lets a caller order by the CLR property name against the camelCased attribute the
	/// serializer wrote. If neither matches, the caller's name is returned unchanged and the comparison
	/// treats every item as missing the attribute, so the order is left alone rather than scrambled.
	/// </remarks>
	/// <param name="items">The scanned items to inspect.</param>
	/// <param name="requestedName">The attribute name the caller asked to order by.</param>
	/// <returns>The stored attribute key to order on.</returns>
	private static string ResolveAttributeName(List<Dictionary<string, AttributeValue>> items, string requestedName)
	{
		foreach (var item in items)
		{
			if (item.ContainsKey(requestedName))
			{
				return requestedName;
			}

			foreach (var key in item.Keys)
			{
				if (string.Equals(key, requestedName, StringComparison.OrdinalIgnoreCase))
				{
					return key;
				}
			}
		}

		return requestedName;
	}

	/// <summary>
	/// Orders two scanned items by one stored attribute, without materializing either projection.
	/// </summary>
	/// <remarks>
	/// Comparing the stored <see cref="AttributeValue"/> rather than a property of the deserialized
	/// projection keeps this free of reflection, which is required here: this package is trimming- and
	/// AOT-clean, and a name-based property lookup would be neither. Numbers compare numerically so that
	/// 10 sorts after 9 rather than before it, and strings compare ordinally. An item that does not carry
	/// the attribute sorts last in BOTH directions, so a partially populated projection set still has a
	/// total order and reversing the sort never promotes an absent value to the first page.
	/// </remarks>
	/// <param name="left">The first scanned item.</param>
	/// <param name="right">The second scanned item.</param>
	/// <param name="attributeName">The stored attribute to order by.</param>
	/// <param name="descending">Whether the caller asked for descending order.</param>
	/// <returns>A signed value describing the relative order of the two items.</returns>
	private static int CompareByAttribute(
		Dictionary<string, AttributeValue> left,
		Dictionary<string, AttributeValue> right,
		string attributeName,
		bool descending)
	{
		var hasLeft = left.TryGetValue(attributeName, out var leftValue);
		var hasRight = right.TryGetValue(attributeName, out var rightValue);

		if (!hasLeft || !hasRight)
		{
			// Deliberately NOT negated for descending: a missing attribute is not a value that can be
			// "greatest", and letting it lead the descending page would hide the rows the caller asked for.
			return hasLeft == hasRight ? 0 : hasLeft ? -1 : 1;
		}

		var comparison = CompareAttributeValues(leftValue!, rightValue!);
		return descending ? -comparison : comparison;
	}

	/// <summary>
	/// Compares two stored attribute values by their DynamoDB type.
	/// </summary>
	/// <param name="left">The first value.</param>
	/// <param name="right">The second value.</param>
	/// <returns>A signed value describing the relative order, or zero when the types are not comparable.</returns>
	private static int CompareAttributeValues(AttributeValue left, AttributeValue right)
	{
		if (left.N is not null
			&& right.N is not null
			&& decimal.TryParse(left.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber)
			&& decimal.TryParse(right.N, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
		{
			return leftNumber.CompareTo(rightNumber);
		}

		if (left.S is not null && right.S is not null)
		{
			return string.CompareOrdinal(left.S, right.S);
		}

		return 0;
	}

	/// <inheritdoc/>
	public async Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken)
	{
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Count only the projections matching the supplied filter.
		var (filterExpression, names, values) = BuildScanFilter(filters);
		return await ComputeTotalAsync(filterExpression, names, values, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
		//, and the count is never the 1 MB-truncated partial.
		var (startKey, carriedTotal) = DecodeCursor(cursor);

		// DynamoDB Scan `Limit` caps the number of items SCANNED, not MATCHED — it is applied BEFORE the
		// FilterExpression. A single scan with Limit=pageSize can therefore return fewer than pageSize
		// matched items while a continuation key is still set. To return a full page of MATCHED items
		// we scan repeatedly following LastEvaluatedKey, with Limit set to exactly the
		// number still needed — so each call consumes everything it scans up to its LastEvaluatedKey (no
		// skip/duplicate across pages) and never matches more than the page needs (no overshoot).
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
				// Table exhausted before the page filled — this is the final page.
				break;
			}
		}

		// Reuse the carried total on continuation pages; compute it once on the first page by following
		// the COUNT continuation to exhaustion so it is the true total, never a 1 MB-truncated partial.
		var totalRecords = carriedTotal
			?? await ComputeTotalAsync(filterExpression, names, values, cancellationToken).ConfigureAwait(false);

		// Surface a continuation cursor only when the page filled AND DynamoDB signalled more to scan.
		// When the table is exhausted (lastEvaluatedKey is null) the cursor is null, so the walk ends
		// cleanly with no phantom next page.
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
	/// to exhaustion so the result is the true total rather than a 1 MB-truncated partial.
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
	/// (<c>#proj.#type = :projType</c>) with every supplied filter, AND-combined.
	/// Filter attribute names use a dedicated <c>#f{n}</c> prefix and values a <c>:v{n}</c> prefix, so
	/// they never collide with the discriminator placeholders even when a filter key matches the
	/// metadata field name. A null/empty filter yields the discriminator alone.
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
	/// projection property was stored (string → S, bool → BOOL, number → N). An untranslatable
	/// value type throws <see cref="NotSupportedException"/> rather than silently dropping the predicate.
	/// </summary>
	private static AttributeValue ToAttributeValue(object? value)
	{
		return value switch
		{
			// A null filter value is ambiguous (NULL-typed attribute vs. absent attribute) and cannot be
			// translated to an unambiguous equality predicate — signal "can't honor the contract"
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
		catch (Amazon.DynamoDBv2.Model.ResourceNotFoundException)
		{
			try
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
			catch (ResourceInUseException)
			{
				// Multi-instance cold-start race: another instance created (or is creating) the table
				// between our DescribeTable and CreateTable. Benign — the table now exists.
			}
		}

		_tableVerified = true;
	}
}
