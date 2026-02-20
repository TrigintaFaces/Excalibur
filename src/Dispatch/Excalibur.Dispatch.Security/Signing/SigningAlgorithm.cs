// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines supported signing algorithms.
/// </summary>
public enum SigningAlgorithm
{
	/// <summary>
	/// Unknown or unsupported algorithm (for forward compatibility).
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// HMAC with SHA-256.
	/// </summary>
	HMACSHA256 = 1,

	/// <summary>
	/// HMAC with SHA-512.
	/// </summary>
	HMACSHA512 = 2,

	/// <summary>
	/// RSA with SHA-256.
	/// </summary>
	RSASHA256 = 3,

	/// <summary>
	/// RSA-PSS with SHA-256.
	/// </summary>
	RSAPSSSHA256 = 4,

	/// <summary>
	/// ECDSA with SHA-256.
	/// </summary>
	ECDSASHA256 = 5,

	/// <summary>
	/// Ed25519 signature algorithm.
	/// </summary>
	Ed25519 = 6,
}
