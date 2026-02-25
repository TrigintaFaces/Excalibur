// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

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
	public KeyManagementProvider Provider { get; init; } = KeyManagementProvider.Local;

	/// <summary>
	/// Gets the key vault or KMS endpoint URL.
	/// </summary>
	/// <value> The endpoint URL for external key management services. </value>
	[Url]
	public string? EndpointUrl { get; init; }

	/// <summary>
	/// Gets the key vault or KMS authentication configuration.
	/// </summary>
	/// <value> Authentication settings for key management service access. </value>
	public string? AuthenticationConfig { get; init; }

	/// <summary>
	/// Gets the key rotation interval.
	/// </summary>
	/// <value> The time interval between automatic key rotations. Defaults to 90 days. </value>
	public TimeSpan KeyRotationInterval { get; init; } = TimeSpan.FromDays(90);

	/// <summary>
	/// Gets a value indicating whether to use hardware security modules (HSM).
	/// </summary>
	/// <value> True to require HSM-backed key storage, false to allow software keys. </value>
	public bool RequireHsm { get; init; }
}
