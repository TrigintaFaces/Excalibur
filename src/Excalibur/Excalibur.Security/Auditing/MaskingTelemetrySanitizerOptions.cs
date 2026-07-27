// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security;

/// <summary>
/// Configuration options for the security-audit masking telemetry sanitizer.
/// </summary>
/// <remarks>
/// The masking sanitizer is safe-by-default with zero configuration. Setting <see cref="Pepper"/> upgrades
/// its tag fingerprints from an unkeyed SHA-256 digest to keyed HMAC-SHA-256, protecting low-entropy
/// identifiers (short user IDs, source IPs) against brute-force and rainbow-table attacks. The pepper is
/// optional: when it is <see langword="null"/>, fingerprinting falls back to the unkeyed digest and never
/// throws (fail-open on the telemetry/audit path).
/// </remarks>
public sealed class MaskingTelemetrySanitizerOptions
{
	/// <summary>
	/// Gets or sets an optional secret pepper (key) used to derive tag fingerprints with HMAC-SHA-256.
	/// </summary>
	/// <value>
	/// A high-entropy secret key sourced from a secret manager / KMS, or <see langword="null"/> (the default)
	/// to fall back to an unkeyed SHA-256 fingerprint. When set, low-entropy identifiers are protected against
	/// brute-force and rainbow-table attacks; when <see langword="null"/>, fingerprints remain correlation
	/// aids only. Fingerprinting never throws regardless of this setting.
	/// </value>
	public byte[]? Pepper { get; set; }
}
