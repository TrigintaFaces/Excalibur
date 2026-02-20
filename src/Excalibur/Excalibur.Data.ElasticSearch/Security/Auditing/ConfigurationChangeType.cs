// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines configuration change types.
/// </summary>
public enum ConfigurationChangeType
{
	/// <summary>
	/// Security-related configuration changes.
	/// </summary>
	SecuritySettings = 0,

	/// <summary>
	/// Authentication configuration changes.
	/// </summary>
	AuthenticationSettings = 1,

	/// <summary>
	/// Authorization configuration changes.
	/// </summary>
	AuthorizationSettings = 2,

	/// <summary>
	/// Encryption configuration changes.
	/// </summary>
	EncryptionSettings = 3,

	/// <summary>
	/// Network configuration changes.
	/// </summary>
	NetworkSettings = 4,

	/// <summary>
	/// Application configuration changes.
	/// </summary>
	ApplicationSettings = 5,

	/// <summary>
	/// Other configuration changes not covered by specific categories.
	/// </summary>
	Other = 6,
}
