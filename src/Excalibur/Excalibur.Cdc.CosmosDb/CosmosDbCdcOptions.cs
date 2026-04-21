// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Configuration options for CosmosDb CDC processor.
/// </summary>
/// <remarks>
/// <para>
/// Change Feed processing properties are in <see cref="ChangeFeed"/>.
/// This follows the Azure Cosmos DB ChangeFeedProcessor configuration pattern.
/// </para>
/// </remarks>
public sealed class CosmosDbCdcOptions
{
	private static readonly CompositeFormat PropertyRequiredFormat =
		CompositeFormat.Parse("{0} is required.");

	private static readonly CompositeFormat PropertyMustBeGreaterThanZeroFormat =
		CompositeFormat.Parse("{0} must be greater than zero.");

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
	public List<string>? PartitionKeyValues { get; set; }

	/// <summary>
	/// Gets or sets the Change Feed processing options.
	/// </summary>
	/// <value> The Cosmos DB Change Feed options. </value>
	public CosmosDbChangeFeedOptions ChangeFeed { get; set; } = new();

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

		if (ChangeFeed.MaxBatchSize <= 0)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(ChangeFeed.MaxBatchSize)));
		}

		if (ChangeFeed.PollInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(ChangeFeed.PollInterval)));
		}

		if (ChangeFeed.MaxWaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyMustBeGreaterThanZeroFormat, nameof(ChangeFeed.MaxWaitTime)));
		}
	}
}
