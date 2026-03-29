// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Specifies the Cedar policy evaluation mode.
/// </summary>
public enum CedarMode
{
	/// <summary>
	/// Local Cedar agent accessible via HTTP.
	/// </summary>
	Local = 0,

	/// <summary>
	/// Amazon Verified Permissions (AVP) accessible via HTTP.
	/// </summary>
	AwsVerifiedPermissions = 1,
}
