// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Projections;

/// <summary>
/// Configuration options for the Cosmos DB projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports both connection string and endpoint/key authentication patterns
/// via the shared <see cref="Client"/> options.
/// The projection store uses <c>projectionType</c> as the partition key for efficient queries.
/// </para>
/// </remarks>
public sealed class CosmosDbProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the shared client/connection options.
	/// </summary>
	public CosmosDbClientOptions Client { get; set; } = new();

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>Defaults to "excalibur".</value>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the container name for projections.
	/// </summary>
	/// <value>Defaults to "projections".</value>
	[Required]
	public string ContainerName { get; set; } = "projections";

	/// <summary>
	/// Gets or sets the partition key path.
	/// </summary>
	/// <remarks>
	/// Uses projectionType as partition key for efficient queries within projection type boundaries.
	/// </remarks>
	/// <value>Defaults to "/projectionType".</value>
	[Required]
	public string PartitionKeyPath { get; set; } = "/projectionType";

	/// <summary>
	/// Gets or sets a value indicating whether to create the container if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateContainerIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets the throughput for the container when created.
	/// </summary>
	/// <value>Defaults to 400 RU/s.</value>
	[Range(1, int.MaxValue)]
	public int ContainerThroughput { get; set; } = 400;

	/// <summary>
	/// Gets or sets the default time to live for projections in seconds.
	/// </summary>
	/// <remarks>
	/// Set to -1 for no expiration (default). Set to a positive value to enable automatic cleanup.
	/// </remarks>
	/// <value>Defaults to -1 (no expiration).</value>
	public int DefaultTtlSeconds { get; set; } = -1;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		Client.Validate();

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException(ErrorMessages.DatabaseNameRequired);
		}

		if (string.IsNullOrWhiteSpace(ContainerName))
		{
			throw new InvalidOperationException(ErrorMessages.ContainerNameRequired);
		}
	}
}
