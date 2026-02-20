// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Specifies the column encryption setting for Always Encrypted.
/// </summary>
public enum SqlConnectionColumnEncryptionSetting
{
	/// <summary>
	/// Column encryption is disabled.
	/// </summary>
	Disabled = 0,

	/// <summary>
	/// Column encryption is enabled.
	/// </summary>
	Enabled = 1,
}
