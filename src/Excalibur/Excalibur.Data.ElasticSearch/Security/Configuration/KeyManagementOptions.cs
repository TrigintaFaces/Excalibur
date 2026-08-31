// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures key management system integration.
/// </summary>
public sealed class KeyManagementOptions
{
	/// <summary>
	/// Gets the key management provider type.
	/// </summary>
	/// <value> The type of key management system to use. </value>
	/// <remarks>
	/// The provider is selected while services are being registered, from the
	/// <c>Elasticsearch:Security:Encryption:KeyManagement:Provider</c> configuration value, so it must be
	/// supplied through configuration. Assigning it in code after registration does not change which
	/// provider was composed.
	/// </remarks>
	public KeyManagementProvider Provider { get; init; } = KeyManagementProvider.Local;

	/// <summary>
	/// Gets the key rotation interval.
	/// </summary>
	/// <value> The time interval between automatic key rotations. Defaults to 90 days. </value>
	public TimeSpan KeyRotationInterval { get; init; } = TimeSpan.FromDays(90);
}
