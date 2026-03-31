// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Amazon.DynamoDBv2;
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
	private const string DataAttribute = "Data";
	private const string ProjectionTypeAttribute = "ProjectionType";

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

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		await EnsureTableAsync(cancellationToken).ConfigureAwait(false);

		var json = JsonSerializer.Serialize(projection, _jsonOptions);
		var item = CreateKey(id);
		item[DataAttribute] = new AttributeValue { S = json };
		item[ProjectionTypeAttribute] = new AttributeValue { S = _projectionType };

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

		// Use Scan with filter expression for projection type
		var request = new ScanRequest
		{
			TableName = _options.TableName,
			FilterExpression = $"{ProjectionTypeAttribute} = :projType",
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
			FilterExpression = $"{ProjectionTypeAttribute} = :projType",
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

	private TProjection? DeserializeItem(Dictionary<string, AttributeValue> item)
	{
		if (!item.TryGetValue(DataAttribute, out var dataAttr) || dataAttr.S is null)
		{
			return null;
		}

		return JsonSerializer.Deserialize<TProjection>(dataAttr.S, _jsonOptions);
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
