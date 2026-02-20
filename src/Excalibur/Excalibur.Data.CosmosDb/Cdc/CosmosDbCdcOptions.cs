// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Text;

using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Configuration options for CosmosDb CDC processor.
/// </summary>
public sealed class CosmosDbCdcOptions
{
	private static readonly CompositeFormat PropertyRequiredFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyIsRequired);

	private static readonly CompositeFormat PropertyMustBeGreaterThanZeroFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyMustBeGreaterThanZero);

	/// <summary>
	/// Gets or sets the CosmosDb connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database identifier.
	/// </summary>
	[Required]
	public string DatabaseId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the container identifier.
	/// </summary>
	[Required]
	public string ContainerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique processor name for position tracking.
	/// </summary>
	[Required]
	public string ProcessorName { get; set; } = "cdc-processor";

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
	/// Gets or sets the partition key path for filtering.
	/// </summary>
	/// <remarks>
	/// If specified, only changes from this partition are processed.
	/// Example: "/tenantId" or "/category".
	/// </remarks>
	public string? PartitionKeyPath { get; set; }

	/// <summary>
	/// Gets or sets specific partition key values to filter.
	/// </summary>
	/// <remarks>
	/// If null, all partitions are processed.
	/// </remarks>
	public IReadOnlyList<string>? PartitionKeyValues { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include the _ts system property.
	/// </summary>
	public bool IncludeTimestamp { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include the _lsn property.
	/// </summary>
	public bool IncludeLsn { get; set; } = true;

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ConnectionString)));
		}

		if (string.IsNullOrWhiteSpace(DatabaseId))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(DatabaseId)));
		}

		if (string.IsNullOrWhiteSpace(ContainerId))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ContainerId)));
		}

		if (string.IsNullOrWhiteSpace(ProcessorName))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ProcessorName)));
		}

		if (MaxBatchSize <= 0)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(MaxBatchSize)));
		}

		if (PollInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(PollInterval)));
		}

		if (MaxWaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(MaxWaitTime)));
		}
	}
}
