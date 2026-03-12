// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Resilience and performance configuration for Cosmos DB client.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>CosmosClientOptions</c> pattern of separating retry and timeout settings
/// from core connection configuration.
/// </para>
/// </remarks>
public sealed class CosmosDbClientResilienceOptions
{
	/// <summary>
	/// Gets or sets the maximum retry attempts for transient failures.
	/// </summary>
	/// <value>Defaults to 9.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 9;

	/// <summary>
	/// Gets or sets the maximum retry wait time in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryWaitTimeInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int RequestTimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable content response on write operations.
	/// </summary>
	/// <remarks>
	/// Setting this to false reduces RU consumption for write operations.
	/// </remarks>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool EnableContentResponseOnWrite { get; set; }
}
