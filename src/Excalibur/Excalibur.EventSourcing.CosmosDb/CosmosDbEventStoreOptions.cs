// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Configuration options for the Cosmos DB event store.
/// </summary>
public sealed class CosmosDbEventStoreOptions
{
	/// <summary>
	/// Gets or sets the events container name.
	/// </summary>
	/// <value>Defaults to "events".</value>
	[Required]
	public string EventsContainerName { get; set; } = "events";

	/// <summary>
	/// Gets or sets the partition key path.
	/// </summary>
	/// <value>Defaults to "/streamId".</value>
	[Required]
	public string PartitionKeyPath { get; set; } = "/streamId";

	/// <summary>
	/// Gets or sets the default time-to-live for events in seconds.
	/// </summary>
	/// <value>-1 for no expiration (default).</value>
	public int DefaultTimeToLiveSeconds { get; set; } = -1;

	/// <summary>
	/// Gets or sets a value indicating whether to use transactions for appending events.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseTransactionalBatch { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum batch size for change feed processing.
	/// </summary>
	/// <value>Defaults to 100.</value>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the poll interval for change feed in milliseconds.
	/// </summary>
	/// <value>Defaults to 1000ms.</value>
	[Range(1, int.MaxValue)]
	public int ChangeFeedPollIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets the throughput for the events container (RU/s).
	/// </summary>
	/// <value>Defaults to 400 RU/s.</value>
	[Range(1, int.MaxValue)]
	public int ContainerThroughput { get; set; } = 400;
}
