// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Text;

using Excalibur.Data.CosmosDb.Resources;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Configuration options for Cosmos DB authorization stores.
/// </summary>
public sealed class CosmosDbAuthorizationOptions
{
	private static readonly CompositeFormat PropertyRequiredFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyIsRequired);

	/// <summary>
	/// Gets or sets the Cosmos DB account endpoint URI.
	/// </summary>
	/// <value>The account endpoint. Required if ConnectionString is not provided.</value>
	public string? AccountEndpoint { get; set; }

	/// <summary>
	/// Gets or sets the Cosmos DB account key.
	/// </summary>
	/// <value>The account key. Required if ConnectionString is not provided.</value>
	public string? AccountKey { get; set; }

	/// <summary>
	/// Gets or sets the connection string (alternative to AccountEndpoint + AccountKey).
	/// </summary>
	/// <value>The connection string.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>The database name. Defaults to "authorization".</value>
	[Required]
	public string DatabaseName { get; set; } = "authorization";

	/// <summary>
	/// Gets or sets the container name for grants.
	/// </summary>
	/// <value>The grants container name. Defaults to "grants".</value>
	[Required]
	public string GrantsContainerName { get; set; } = "grants";

	/// <summary>
	/// Gets or sets the container name for activity groups.
	/// </summary>
	/// <value>The activity groups container name. Defaults to "activity-groups".</value>
	[Required]
	public string ActivityGroupsContainerName { get; set; } = "activity-groups";

	/// <summary>
	/// Gets or sets the consistency level for operations.
	/// </summary>
	/// <value>The consistency level. Defaults to null (use account default).</value>
	public ConsistencyLevel? ConsistencyLevel { get; set; }

	/// <summary>
	/// Gets or sets the maximum retry attempts for transient failures.
	/// </summary>
	/// <value>The maximum retry attempts. Defaults to 9.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 9;

	/// <summary>
	/// Gets or sets the maximum retry wait time in seconds.
	/// </summary>
	/// <value>The maximum retry wait time. Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int MaxRetryWaitTimeInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>The request timeout. Defaults to 60 seconds.</value>
	[Range(1, int.MaxValue)]
	public int RequestTimeoutInSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets a value indicating whether to use direct connection mode.
	/// </summary>
	/// <value><see langword="true"/> to use direct mode; otherwise, <see langword="false"/>. Defaults to true.</value>
	public bool UseDirectMode { get; set; } = true;

	/// <summary>
	/// Gets or sets the preferred regions for geo-redundant operations.
	/// </summary>
	/// <value>The list of preferred regions.</value>
	public IReadOnlyList<string>? PreferredRegions { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content response on write operations.
	/// </summary>
	/// <value><see langword="true"/> to enable content response on write; otherwise, <see langword="false"/>.</value>
	/// <remarks>
	/// Setting this to false reduces RU consumption for write operations.
	/// </remarks>
	public bool EnableContentResponseOnWrite { get; set; }

	/// <summary>
	/// Gets or sets the HTTP client factory for custom HTTP client configuration.
	/// </summary>
	/// <value>The HTTP client factory function.</value>
	/// <remarks>
	/// Use this to customize the HTTP client, such as disabling SSL validation
	/// for local development with the Cosmos DB emulator.
	/// </remarks>
	public Func<HttpClient>? HttpClientFactory { get; set; }

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
	public void Validate()
	{
		var hasConnectionString = !string.IsNullOrWhiteSpace(ConnectionString);
		var hasEndpointAndKey = !string.IsNullOrWhiteSpace(AccountEndpoint) && !string.IsNullOrWhiteSpace(AccountKey);

		if (!hasConnectionString && !hasEndpointAndKey)
		{
			throw new InvalidOperationException(
				ErrorMessages.EitherConnectionStringOrAccountEndpointRequired);
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(DatabaseName)));
		}

		if (string.IsNullOrWhiteSpace(GrantsContainerName))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(GrantsContainerName)));
		}

		if (string.IsNullOrWhiteSpace(ActivityGroupsContainerName))
		{
			throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, PropertyRequiredFormat, nameof(ActivityGroupsContainerName)));
		}
	}
}
