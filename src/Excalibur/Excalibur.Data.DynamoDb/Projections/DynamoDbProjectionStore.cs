// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
public sealed class DynamoDbProjectionStore<TProjection> : IProjectionStore<TProjection>
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
		_jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
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

#pragma warning disable IL2026
		return JsonSerializer.Deserialize<TProjection>(json, _jsonOptions);
#pragma warning restore IL2026
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		// Serialize projection to JSON, then convert to flat DynamoDB attributes.
		// Projection properties live at the item root. Framework metadata is isolated
		// under a nested '_projection' map to prevent field name collisions.
#pragma warning disable IL2026
		var json = JsonSerializer.Serialize(projection, _jsonOptions);
#pragma warning restore IL2026
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

		// Use Scan with filter expression for projection type (nested under metadata key)
		var request = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = "#proj.#type = :projType",
			ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#proj"] = MetadataKey,
				["#type"] = MetaFieldType,
			},
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":projType"] = new() { S = _projectionType },
			},
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

		var request = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = "#proj.#type = :projType",
			ExpressionAttributeNames = new Dictionary<string, string>
			{
				["#proj"] = MetadataKey,
				["#type"] = MetaFieldType,
			},
			ExpressionAttributeValues = new Dictionary<string, AttributeValue>
			{
				[":projType"] = new() { S = _projectionType },
			},
			Select = Select.COUNT,
		};

		var response = await _client.ScanAsync(request, cancellationToken).ConfigureAwait(false);
		return response.Count ?? 0;
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
