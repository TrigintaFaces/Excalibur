// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Connection options for the Cosmos DB outbox store.
/// </summary>
/// <remarks>
/// Either <see cref="ConnectionString"/> or both <see cref="AccountEndpoint"/> and
/// <see cref="AccountKey"/> must be provided.
/// </remarks>
public sealed class CosmosDbOutboxConnectionOptions
{
	/// <summary>
	/// Gets or sets the Azure Cosmos DB connection string.
	/// </summary>
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the Azure Cosmos DB account endpoint.
	/// </summary>
	public string? AccountEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the Azure Cosmos DB account key.
	/// </summary>
	public string? AccountKey { get; set; }
}
