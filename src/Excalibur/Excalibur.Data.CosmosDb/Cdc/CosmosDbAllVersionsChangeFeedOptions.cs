// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Configuration options for the AllVersionsAndDeletes change feed processor.
/// </summary>
/// <remarks>
/// <para>
/// The AllVersionsAndDeletes mode captures all changes including deletes, with
/// before/after state for update operations. This mode requires the container
/// to have the change feed policy configured with a full fidelity retention window.
/// </para>
/// <para>
/// Prerequisites:
/// <list type="bullet">
/// <item><description>Azure Cosmos DB SDK 3.50.0 or later</description></item>
/// <item><description>Container configured with <c>ChangeFeedPolicy.FullFidelityRetention</c></description></item>
/// <item><description>Serverless or provisioned throughput account</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class CosmosDbAllVersionsChangeFeedOptions
{
	/// <summary>
	/// Gets or sets the lease container name for change feed processor coordination.
	/// </summary>
	/// <value>The lease container name. Default is "leases".</value>
	/// <remarks>
	/// The lease container must exist in the same database. It is used for
	/// distributed coordination when multiple processor instances share workload.
	/// </remarks>
	[Required]
	public string LeaseContainer { get; set; } = "leases";

	/// <summary>
	/// Gets or sets the interval between change feed polls when no changes are detected.
	/// </summary>
	/// <value>The feed poll interval. Default is 5 seconds.</value>
	public TimeSpan FeedPollInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the start time for change feed processing.
	/// </summary>
	/// <value>
	/// The start time, or <see langword="null"/> to start from the last checkpoint.
	/// Default is <see langword="null"/>.
	/// </value>
	/// <remarks>
	/// If set, the processor ignores any previously saved checkpoint and starts
	/// reading changes from this timestamp. This is useful for reprocessing scenarios.
	/// </remarks>
	public DateTimeOffset? StartTime { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of items per change feed batch.
	/// </summary>
	/// <value>The maximum batch size. Default is 100.</value>
	[Range(1, 10000)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the unique processor name for distributed coordination.
	/// </summary>
	/// <value>The processor name. Default is "all-versions-cdc-processor".</value>
	[Required]
	public string ProcessorName { get; set; } = "all-versions-cdc-processor";
}
