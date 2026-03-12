// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Text;

using Excalibur.Data.CosmosDb.Resources;

namespace Excalibur.Data.CosmosDb.Authorization;

/// <summary>
/// Configuration options for Cosmos DB authorization stores.
/// </summary>
/// <remarks>
/// <para>
/// Client/connection properties are delegated to <see cref="Client"/>.
/// This follows the <c>CosmosClientOptions</c> pattern of reusing shared client configuration.
/// </para>
/// </remarks>
public sealed class CosmosDbAuthorizationOptions
{
	private static readonly CompositeFormat PropertyRequiredFormat =
		CompositeFormat.Parse(ErrorMessages.PropertyIsRequired);

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
	/// Gets or sets the shared client/connection options.
	/// </summary>
	/// <value> The Cosmos DB client options. </value>
	public CosmosDbClientOptions Client { get; set; } = new();

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
	public void Validate()
	{
		Client.Validate();

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
