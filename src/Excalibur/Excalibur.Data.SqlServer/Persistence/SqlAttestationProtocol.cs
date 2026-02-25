// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Specifies the attestation protocol for Always Encrypted with secure enclaves.
/// </summary>
public enum SqlAttestationProtocol
{
	/// <summary>
	/// No attestation protocol specified.
	/// </summary>
	NotSpecified = 0,

	/// <summary>
	/// Azure Attestation Service protocol.
	/// </summary>
	AAS = 1,

	/// <summary>
	/// Host Guardian Service protocol.
	/// </summary>
	HGS = 2,

	/// <summary>
	/// No attestation required.
	/// </summary>
	None = 3,
}
