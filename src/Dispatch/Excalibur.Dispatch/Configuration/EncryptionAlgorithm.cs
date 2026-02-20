// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Defines the supported encryption algorithms.
/// </summary>
public enum EncryptionAlgorithm
{
	/// <summary>
	/// No encryption.
	/// </summary>
	None = 0,

	/// <summary>
	/// AES-128-GCM.
	/// </summary>
	Aes128Gcm = 1,

	/// <summary>
	/// AES-256-GCM.
	/// </summary>
	Aes256Gcm = 2,
}
