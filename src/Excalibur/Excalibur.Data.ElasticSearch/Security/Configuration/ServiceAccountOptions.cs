// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures service account authentication for machine-to-machine scenarios.
/// </summary>
public sealed class ServiceAccountOptions
{
	/// <summary>
	/// Gets a value indicating whether service account authentication is enabled.
	/// </summary>
	/// <value> True to enable service account authentication, false otherwise. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets the service account identifier.
	/// </summary>
	/// <value> The unique identifier for the service account. </value>
	public string? AccountId { get; init; }
}
