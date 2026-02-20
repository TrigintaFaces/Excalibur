// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Configuration options for DynamoDB CDC processor.
/// </summary>
public sealed class DynamoDbCdcOptions
{
	/// <summary>
	/// Gets or sets the DynamoDB table name.
	/// </summary>
	public string TableName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the stream ARN.
	/// </summary>
	/// <remarks>
	/// If not specified, the stream ARN is auto-discovered from the table.
	/// </remarks>
	public string? StreamArn { get; set; }

	/// <summary>
	/// Gets or sets the unique processor name for position tracking.
	/// </summary>
	[Required]
	public string ProcessorName { get; set; } = "cdc-processor";

	/// <summary>
	/// Gets or sets the maximum number of records per batch.
	/// </summary>
	/// <remarks>
	/// DynamoDB Streams supports up to 1000 records per GetRecords call.
	/// </remarks>
	[Range(1, 1000)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the interval between stream polls when no changes are available.
	/// </summary>
	/// <remarks>
	/// DynamoDB Streams supports up to 5 GetRecords calls per second per shard.
	/// </remarks>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the starting position for stream processing.
	/// </summary>
	/// <remarks>
	/// If null, uses the last confirmed position from the state store,
	/// or starts from TRIM_HORIZON if no position is found.
	/// </remarks>
	public DynamoDbCdcPosition? StartPosition { get; set; }

	/// <summary>
	/// Gets or sets the expected stream view type.
	/// </summary>
	/// <remarks>
	/// This is informational; the actual view type is determined by table configuration.
	/// Set this to match your table's StreamSpecification.StreamViewType.
	/// </remarks>
	public DynamoDbStreamViewType StreamViewType { get; set; } = DynamoDbStreamViewType.NewAndOldImages;

	/// <summary>
	/// Gets or sets the maximum time to wait for records before returning an empty batch.
	/// </summary>
	public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to auto-discover new shards.
	/// </summary>
	/// <remarks>
	/// When enabled, the processor periodically checks for new shards created by resharding.
	/// </remarks>
	public bool AutoDiscoverShards { get; set; } = true;

	/// <summary>
	/// Gets or sets the interval for shard discovery.
	/// </summary>
	public TimeSpan ShardDiscoveryInterval { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(TableName) && string.IsNullOrWhiteSpace(StreamArn))
		{
			throw new InvalidOperationException($"Either {nameof(TableName)} or {nameof(StreamArn)} is required.");
		}

		if (string.IsNullOrWhiteSpace(ProcessorName))
		{
			throw new InvalidOperationException($"{nameof(ProcessorName)} is required.");
		}

		if (MaxBatchSize is <= 0 or > 1000)
		{
			throw new InvalidOperationException($"{nameof(MaxBatchSize)} must be between 1 and 1000.");
		}

		if (PollInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(PollInterval)} must be greater than zero.");
		}

		if (MaxWaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(MaxWaitTime)} must be greater than zero.");
		}

		if (ShardDiscoveryInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(ShardDiscoveryInterval)} must be greater than zero.");
		}
	}
}
