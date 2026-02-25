// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.DynamoDb.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// DynamoDB-backed state store for CDC position tracking.
/// </summary>
/// <remarks>
/// Uses a DynamoDB table to store CDC positions. The table should have
/// a primary key 'pk' (string) for the processor name.
/// </remarks>
public sealed partial class DynamoDbCdcStateStore : IDynamoDbCdcStateStore
{
	private const string PkAttribute = "pk";
	private const string PositionDataAttribute = "positionData";
	private const string UpdatedAtAttribute = "updatedAt";
	private const string EventCountAttribute = "eventCount";

	private readonly IAmazonDynamoDB _dynamoClient;
	private readonly string _tableName;
	private readonly ILogger<DynamoDbCdcStateStore> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbCdcStateStore"/> class.
	/// </summary>
	/// <param name="dynamoClient">The DynamoDB client.</param>
	/// <param name="tableName">The table name for storing CDC state.</param>
	/// <param name="logger">The logger.</param>
	public DynamoDbCdcStateStore(
			IAmazonDynamoDB dynamoClient,
			string tableName,
			ILogger<DynamoDbCdcStateStore> logger)
			: this(dynamoClient, new DynamoDbCdcStateStoreOptions { TableName = tableName }, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbCdcStateStore"/> class with options.
	/// </summary>
	/// <param name="dynamoClient">The DynamoDB client.</param>
	/// <param name="options">The CDC state store options.</param>
	/// <param name="logger">The logger.</param>
	public DynamoDbCdcStateStore(
			IAmazonDynamoDB dynamoClient,
			IOptions<DynamoDbCdcStateStoreOptions> options,
			ILogger<DynamoDbCdcStateStore> logger)
			: this(dynamoClient, options?.Value ?? throw new ArgumentNullException(nameof(options)), logger)
	{
	}

	private DynamoDbCdcStateStore(
			IAmazonDynamoDB dynamoClient,
			DynamoDbCdcStateStoreOptions options,
			ILogger<DynamoDbCdcStateStore> logger)
	{
		_dynamoClient = dynamoClient ?? throw new ArgumentNullException(nameof(dynamoClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(options);
		options.Validate();
		_tableName = options.TableName;
	}

	/// <inheritdoc/>
	public async Task<DynamoDbCdcPosition?> GetPositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		var request = new GetItemRequest
		{
			TableName = _tableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[PkAttribute] = new AttributeValue { S = processorName },
			},
			ConsistentRead = true,
		};

		GetItemResponse response;
		try
		{
			response = await _dynamoClient.GetItemAsync(request, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogDynamoDbError(nameof(GetPositionAsync), processorName, _tableName, ex);
			throw;
		}

		if (!response.IsItemSet || response.Item.Count == 0)
		{
			LogPositionNotFound(processorName);
			return null;
		}

		if (!response.Item.TryGetValue(PositionDataAttribute, out var positionData) ||
			string.IsNullOrEmpty(positionData.S))
		{
			LogPositionDataMissing(processorName);
			return null;
		}

		if (!DynamoDbCdcPosition.TryFromBase64(positionData.S, out var position))
		{
			LogPositionParseFailed(processorName);
			return null;
		}

		LogPositionLoaded(processorName, position.ShardPositions.Count);
		return position;
	}

	/// <inheritdoc/>
	public async Task SavePositionAsync(
		string processorName,
		DynamoDbCdcPosition position,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		ArgumentNullException.ThrowIfNull(position);

		var positionData = position.ToBase64();

		var request = new PutItemRequest
		{
			TableName = _tableName,
			Item = new Dictionary<string, AttributeValue>
			{
				[PkAttribute] = new AttributeValue { S = processorName },
				[PositionDataAttribute] = new AttributeValue { S = positionData },
				[UpdatedAtAttribute] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
				[EventCountAttribute] = new AttributeValue { N = position.ShardPositions.Count.ToString() },
			},
		};

		try
		{
			_ = await _dynamoClient.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogDynamoDbError(nameof(SavePositionAsync), processorName, _tableName, ex);
			throw;
		}

		LogPositionSaved(processorName, position.ShardPositions.Count);
	}

	/// <inheritdoc/>
	public async Task DeletePositionAsync(
		string processorName,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);

		var request = new DeleteItemRequest
		{
			TableName = _tableName,
			Key = new Dictionary<string, AttributeValue>
			{
				[PkAttribute] = new AttributeValue { S = processorName },
			},
		};

		try
		{
			_ = await _dynamoClient.DeleteItemAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogDynamoDbError(nameof(DeletePositionAsync), processorName, _tableName, ex);
			throw;
		}

		LogPositionDeleted(processorName);
	}

	/// <inheritdoc/>
	async Task<ChangePosition?> ICdcStateStore.GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
		await GetPositionAsync(consumerId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	Task ICdcStateStore.SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);

		if (position is not DynamoDbCdcPosition dynamoPosition)
		{
			dynamoPosition = DynamoDbCdcPosition.FromBase64(position.ToToken());
		}

		return SavePositionAsync(consumerId, dynamoPosition, cancellationToken);
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

		var request = new ScanRequest { TableName = _tableName };
		ScanResponse response;
		try
		{
			response = await _dynamoClient.ScanAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (AmazonDynamoDBException ex)
		{
			LogDynamoDbError(nameof(ICdcStateStore.GetAllPositionsAsync), "*", _tableName, ex);
			throw;
		}

		foreach (var item in response.Items)
		{
			if (item.TryGetValue(PkAttribute, out var pk) &&
				item.TryGetValue(PositionDataAttribute, out var posData) &&
				DynamoDbCdcPosition.TryFromBase64(posData.S, out var position) &&
				position is not null)
			{
				yield return (pk.S, position);
			}
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_disposed = true;
	}

	[LoggerMessage(DataDynamoDbEventId.CdcPositionNotFound, LogLevel.Debug, "No saved position found for processor '{ProcessorName}'")]
	private partial void LogPositionNotFound(string processorName);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionDataMissing, LogLevel.Warning, "Position data attribute missing for processor '{ProcessorName}'")]
	private partial void LogPositionDataMissing(string processorName);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionParseFailed, LogLevel.Warning, "Failed to parse position data for processor '{ProcessorName}'")]
	private partial void LogPositionParseFailed(string processorName);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionLoaded, LogLevel.Debug, "Loaded position for processor '{ProcessorName}' with {ShardCount} shards")]
	private partial void LogPositionLoaded(string processorName, int shardCount);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionSaved, LogLevel.Debug, "Saved position for processor '{ProcessorName}' with {ShardCount} shards")]
	private partial void LogPositionSaved(string processorName, int shardCount);

	[LoggerMessage(DataDynamoDbEventId.CdcPositionDeleted, LogLevel.Information, "Deleted position for processor '{ProcessorName}'")]
	private partial void LogPositionDeleted(string processorName);

	[LoggerMessage(DataDynamoDbEventId.CdcStateStoreError, LogLevel.Error,
		"DynamoDB error in {Operation} for processor '{ProcessorName}' on table '{TableName}'")]
	private partial void LogDynamoDbError(string operation, string processorName, string tableName, Exception exception);
}
