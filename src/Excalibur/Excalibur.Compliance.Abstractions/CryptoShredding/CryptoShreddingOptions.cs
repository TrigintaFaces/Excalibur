// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Configuration for crypto-shredding: per-subject field encryption and key-lifecycle erasure.
/// </summary>
public sealed class CryptoShreddingOptions
{
	/// <summary>
	/// Gets or sets the identifier of the encryption algorithm used when producing new envelopes.
	/// Defaults to <c>1</c> (AES-256-GCM).
	/// </summary>
	public byte DefaultAlgorithmId { get; set; } = 1;
}
