// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Configuration options for the Azure Cosmos DB data provider.
/// </summary>
public sealed class CosmosDbOptions
{
	/// <summary>
	/// Gets or sets the shared client/connection options.
	/// </summary>
	public CosmosDbClientOptions Client { get; set; } = new();

	/// <summary>
	/// Gets or sets the name of the provider instance.
	/// </summary>
	[Required]
	public string Name { get; set; } = "CosmosDb";

	/// <summary>
	/// Gets or sets the default database name.
	/// </summary>
	public string? DatabaseName { get; set; }

	/// <summary>
	/// Gets or sets the default container name.
	/// </summary>
	public string? DefaultContainerName { get; set; }

	/// <summary>
	/// Gets or sets the default partition key path.
	/// </summary>
	[Required]
	public string DefaultPartitionKeyPath { get; set; } = "/id";

	/// <summary>
	/// Gets or sets the maximum number of connections per endpoint.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxConnectionsPerEndpoint { get; set; } = 50;

	/// <summary>
	/// Gets or sets the idle connection timeout in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int IdleConnectionTimeoutInSeconds { get; set; } = 600;

	/// <summary>
	/// Gets or sets a value indicating whether to enable TCP connection reuse.
	/// </summary>
	public bool EnableTcpConnectionEndpointRediscovery { get; set; } = true;

	/// <summary>
	/// Gets or sets the bulk execution max degree of parallelism.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int BulkExecutionMaxDegreeOfParallelism { get; set; } = 25;

	/// <summary>
	/// Gets or sets a value indicating whether to allow bulk execution.
	/// </summary>
	public bool AllowBulkExecution { get; set; }

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
	}
}
