// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.MaterializedViews;

/// <summary>
/// Authentication options for Elasticsearch materialized view store.
/// </summary>
public sealed class ElasticSearchAuthOptions
{
	/// <summary>
	/// Gets or sets the username for basic authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no authentication).</value>
	public string? Username { get; set; }

	/// <summary>
	/// Gets or sets the password for basic authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no authentication).</value>
	public string? Password { get; set; }

	/// <summary>
	/// Gets or sets the API key for API key authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no API key authentication).</value>
	public string? ApiKey { get; set; }
}
