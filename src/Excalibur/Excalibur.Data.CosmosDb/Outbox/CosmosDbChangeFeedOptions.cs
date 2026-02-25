// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.CosmosDb.Outbox;

/// <summary>
/// Configuration options for the Cosmos DB outbox change feed processor.
/// </summary>
public sealed class CosmosDbChangeFeedOptions
{
	/// <summary>
	/// Gets or sets the lease container name used for change feed processor coordination.
	/// </summary>
	/// <value>The lease container name. Defaults to "outbox-leases".</value>
	[Required]
	public string LeaseContainerName { get; set; } = "outbox-leases";

	/// <summary>
	/// Gets or sets the poll interval for the change feed processor.
	/// </summary>
	/// <value>The feed poll interval. Defaults to 1 second.</value>
	public TimeSpan FeedPollInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of items to process per batch.
	/// </summary>
	/// <value>The maximum items per batch. Defaults to 100.</value>
	[Range(1, 10000)]
	public int MaxItemsPerBatch { get; set; } = 100;

	/// <summary>
	/// Gets or sets the processor instance name for multi-instance coordination.
	/// </summary>
	/// <value>The instance name. Defaults to the machine name.</value>
	public string InstanceName { get; set; } = Environment.MachineName;

	/// <summary>
	/// Gets or sets a value indicating whether to create the lease container if it does not exist.
	/// </summary>
	/// <value><see langword="true"/> to auto-create; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool CreateLeaseContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to start from the beginning of the change feed.
	/// </summary>
	/// <value><see langword="true"/> to start from beginning; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool StartFromBeginning { get; set; }
}
