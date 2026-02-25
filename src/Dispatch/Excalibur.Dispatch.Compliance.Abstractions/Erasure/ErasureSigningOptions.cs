// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for erasure certificate signing using HMAC-SHA256.
/// </summary>
/// <remarks>
/// The signing key is used to generate and verify HMAC signatures on erasure certificates,
/// providing tamper detection for compliance evidence. Configure via:
/// <code>
/// services.Configure&lt;ErasureSigningOptions&gt;(config.GetSection("Erasure:Signing"));
/// </code>
/// </remarks>
public sealed class ErasureSigningOptions
{
	/// <summary>
	/// Gets or sets the HMAC-SHA256 signing key for certificate signatures.
	/// Must be at least 32 bytes for security. Configure from KMS/HSM in production.
	/// </summary>
	public byte[] SigningKey { get; set; } = [];
}
