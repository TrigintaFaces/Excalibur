// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Configuration options for <see cref="HashingTelemetrySanitizer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Controls which telemetry tag names are hashed, suppressed, or passed through unchanged.
/// </para>
/// <para>
/// When <see cref="IncludeRawPii"/> is <see langword="true"/>, all sanitization is bypassed
/// and raw values are emitted. This should only be used in development environments.
/// </para>
/// <para>
/// When <see cref="Pepper"/> is set, sensitive values are fingerprinted with keyed HMAC-SHA-256 instead of
/// an unkeyed SHA-256 digest, protecting low-entropy identifiers against brute-force/rainbow-table attacks.
/// </para>
/// </remarks>
public sealed class TelemetrySanitizerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to bypass all sanitization and emit raw PII values.
	/// </summary>
	/// <value><see langword="true"/> to bypass sanitization (development only); <see langword="false"/> by default.</value>
	public bool IncludeRawPii { get; set; }

	/// <summary>
	/// Gets or sets an optional secret pepper (key) used to derive tag fingerprints with HMAC-SHA-256.
	/// </summary>
	/// <value>
	/// A high-entropy secret key sourced from a secret manager / KMS, or <see langword="null"/> (the default)
	/// to fall back to an unkeyed SHA-256 fingerprint. When set, low-entropy identifiers are protected against
	/// brute-force and rainbow-table attacks; when <see langword="null"/>, fingerprints remain correlation
	/// aids only. Fingerprinting never throws on the telemetry path regardless of this setting.
	/// </value>
	public byte[]? Pepper { get; set; }

	/// <summary>
	/// Gets or sets the tag names whose values should be hashed using SHA-256 before emission.
	/// </summary>
	/// <value>A list of tag names to hash. Defaults to common PII tag names.</value>
	[Required]
	public IList<string> SensitiveTagNames { get; set; } =
	[
		"user.id",
		"user.name",
		"auth.user_id",
		"auth.subject_id",
		"auth.identity_name",
		"auth.tenant_id",
		"audit.user_id",
		"tenant.id",
		"tenant.name",
		"dispatch.messaging.tenant_id",
	];

	/// <summary>
	/// Gets or sets the tag names whose values should be suppressed entirely (tag not emitted).
	/// </summary>
	/// <value>A list of tag names to suppress. Defaults to highly sensitive tag names.</value>
	[Required]
	public IList<string> SuppressedTagNames { get; set; } =
	[
		"auth.email",
		"auth.token",
	];
}
