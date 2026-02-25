// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// Configuration options for the DynamoDB event store.
/// </summary>
public sealed class DynamoDbEventStoreOptions
{
	/// <summary>
	/// Gets or sets the events table name.
	/// </summary>
	/// <value>Defaults to "Events".</value>
	[Required]
	public string EventsTableName { get; set; } = "Events";

	/// <summary>
	/// Gets or sets the partition key attribute name.
	/// </summary>
	/// <value>Defaults to "pk".</value>
	[Required]
	public string PartitionKeyAttribute { get; set; } = "pk";

	/// <summary>
	/// Gets or sets the sort key attribute name.
	/// </summary>
	/// <value>Defaults to "sk".</value>
	[Required]
	public string SortKeyAttribute { get; set; } = "sk";

	/// <summary>
	/// Gets or sets a value indicating whether to use transactions for appending events.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseTransactionalWrite { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum batch size for DynamoDB Streams processing.
	/// </summary>
	/// <value>Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the poll interval for DynamoDB Streams in milliseconds.
	/// </summary>
	/// <value>Defaults to 1000ms.</value>
	[Range(1, int.MaxValue)]
	public int StreamsPollIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to create the table if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateTableIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable DynamoDB Streams.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool EnableStreams { get; set; } = true;

	/// <summary>
	/// Gets or sets throughput configuration for provisioned capacity mode.
	/// </summary>
	public DynamoDbThroughputOptions Throughput { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets the read capacity units for the events table.
	/// </summary>
	/// <value>Defaults to 5 RCU (for provisioned mode).</value>
	[Range(1, int.MaxValue)]
	public int ReadCapacityUnits { get => Throughput.ReadCapacityUnits; set => Throughput.ReadCapacityUnits = value; }

	/// <summary>
	/// Gets or sets the write capacity units for the events table.
	/// </summary>
	/// <value>Defaults to 5 WCU (for provisioned mode).</value>
	[Range(1, int.MaxValue)]
	public int WriteCapacityUnits { get => Throughput.WriteCapacityUnits; set => Throughput.WriteCapacityUnits = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to use on-demand capacity.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseOnDemandCapacity { get => Throughput.UseOnDemandCapacity; set => Throughput.UseOnDemandCapacity = value; }
}

/// <summary>
/// Throughput configuration options for DynamoDB provisioned capacity mode.
/// </summary>
public sealed class DynamoDbThroughputOptions
{
	/// <summary>
	/// Gets or sets the read capacity units for the events table.
	/// </summary>
	/// <value>Defaults to 5 RCU (for provisioned mode).</value>
	[Range(1, int.MaxValue)]
	public int ReadCapacityUnits { get; set; } = 5;

	/// <summary>
	/// Gets or sets the write capacity units for the events table.
	/// </summary>
	/// <value>Defaults to 5 WCU (for provisioned mode).</value>
	[Range(1, int.MaxValue)]
	public int WriteCapacityUnits { get; set; } = 5;

	/// <summary>
	/// Gets or sets a value indicating whether to use on-demand capacity.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseOnDemandCapacity { get; set; } = true;
}
