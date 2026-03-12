// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Change Feed processing options for the CosmosDb CDC processor.
/// </summary>
/// <remarks>
/// Follows the Azure Cosmos DB ChangeFeedProcessor configuration pattern
/// of separating feed processing behavior from connection/container settings.
/// </remarks>
public sealed class CosmosDbChangeFeedOptions
{
	/// <summary>
	/// Gets or sets the Change Feed mode.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="CosmosDbCdcMode.LatestVersion"/>: Captures inserts and updates only.
	/// </para>
	/// <para>
	/// <see cref="CosmosDbCdcMode.AllVersionsAndDeletes"/>: Captures all changes including deletes.
	/// Requires container configuration with changeFeedPolicy.
	/// </para>
	/// </remarks>
	public CosmosDbCdcMode Mode { get; set; } = CosmosDbCdcMode.LatestVersion;

	/// <summary>
	/// Gets or sets the starting position for Change Feed processing.
	/// </summary>
	/// <remarks>
	/// If null, uses the last confirmed position from the state store,
	/// or starts from the beginning if no position is found.
	/// </remarks>
	public CosmosDbCdcPosition? StartPosition { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of items per batch.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the interval between Change Feed polls when no changes are available.
	/// </summary>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum wait time for Change Feed operations.
	/// </summary>
	public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to include the _ts system property.
	/// </summary>
	public bool IncludeTimestamp { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include the _lsn property.
	/// </summary>
	public bool IncludeLsn { get; set; } = true;
}
