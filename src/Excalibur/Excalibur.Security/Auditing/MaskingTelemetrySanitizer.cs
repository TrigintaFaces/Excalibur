// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Options;

namespace Excalibur.Security;

/// <summary>
/// Zero-config, secure-by-default <see cref="ITelemetrySanitizer"/> for the security-audit path. Masks
/// sensitive tag values (such as user IDs and source IPs) with a stable fingerprint and redacts
/// secret-shaped substrings from free-form payloads so raw PII is not written directly to a log sink or
/// telemetry backend.
/// </summary>
/// <remarks>
/// <para>
/// Needs no configuration to be safe-by-default: it pseudonymizes each sensitive tag value with a stable
/// fingerprint, so distinct values stay correlatable across records without emitting the raw value. By
/// default the fingerprint is an <strong>unkeyed</strong> SHA-256 digest — a correlation aid, not a
/// cryptographic guarantee: high-entropy values (long tokens, GUIDs) are well protected, but
/// <strong>low-entropy identifiers</strong> (for example a <c>SourceIp</c> or a short <c>UserId</c>) remain
/// brute-forceable by an attacker who can read the log and hash the candidate domain. To protect low-entropy
/// identifiers cryptographically, configure a secret pepper
/// (<see cref="MaskingTelemetrySanitizerOptions.Pepper"/>): fingerprints are then derived with keyed
/// HMAC-SHA-256, which a dictionary or rainbow-table attack cannot reverse without the pepper. Configuring a
/// pepper never changes the fail-open contract — fingerprinting still never throws on the audit path.
/// Free-form payloads have secret-shaped substrings redacted and are length-capped.
/// </para>
/// <para>
/// This is the default sanitizer registered by the security-auditing services. A richer sanitizer (for
/// example one registered by the observability services) composes over it via a non-<c>Try</c>
/// registration, which wins over the <c>TryAddSingleton</c> default.
/// </para>
/// </remarks>
internal sealed partial class MaskingTelemetrySanitizer : ITelemetrySanitizer
{
	/// <summary>
	/// The shared, immutable singleton instance using the unkeyed (no-pepper) default. The sanitizer holds no
	/// mutable state and is thread-safe.
	/// </summary>
	public static readonly MaskingTelemetrySanitizer Instance = new((byte[]?)null);

	private const int MaxPayloadLength = 4096;
	private const string Redaction = "***REDACTED***";

	private readonly byte[]? _pepper;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaskingTelemetrySanitizer"/> class from configuration.
	/// </summary>
	/// <param name="options">The sanitizer options carrying the optional secret pepper.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	public MaskingTelemetrySanitizer(IOptions<MaskingTelemetrySanitizerOptions> options)
		: this((options ?? throw new ArgumentNullException(nameof(options))).Value.Pepper)
	{
	}

	private MaskingTelemetrySanitizer(byte[]? pepper) =>
		// Copy the caller's array so later mutation of the options cannot alter the derived key.
		_pepper = pepper is { Length: > 0 } value ? (byte[])value.Clone() : null;

	/// <inheritdoc />
	/// <remarks>
	/// Returns a stable <c>sha256:</c> fingerprint of the raw value so distinct values stay distinguishable for
	/// correlation without emitting the raw value. When a pepper is configured the fingerprint is derived with
	/// keyed HMAC-SHA-256, protecting low-entropy identifiers; otherwise it is an unkeyed SHA-256 digest, which
	/// pseudonymizes rather than cryptographically protects — low-entropy identifiers then remain
	/// brute-forceable. A null or empty value is passed through unchanged (nothing sensitive to mask).
	/// </remarks>
	public string? SanitizeTag(string tagName, string? rawValue)
	{
		if (string.IsNullOrEmpty(rawValue))
		{
			return rawValue;
		}

		var utf8 = Encoding.UTF8.GetBytes(rawValue);

		// Keyed HMAC-SHA-256 when a pepper is configured; unkeyed SHA-256 otherwise. Both emit a 32-byte
		// digest, so the truncated tag shape is identical either way. Neither path throws — masking never
		// breaks the audit path (fail-open).
		var hash = _pepper is null
			? SHA256.HashData(utf8)
			: HMACSHA256.HashData(_pepper, utf8);

		// 12 bytes (96 bits) of the digest keeps collisions negligible while staying compact. Audit masking
		// is not a hot path, so a straightforward allocation is preferred over premature span micro-tuning.
		return string.Concat("sha256:", Convert.ToHexStringLower(hash.AsSpan(0, 12)));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Redacts secret-shaped substrings (bearer tokens, long opaque key/token literals) and caps the length
	/// so an oversized or credential-bearing free-form field cannot land raw in a sink.
	/// </remarks>
	public string SanitizePayload(string payload)
	{
		if (string.IsNullOrEmpty(payload))
		{
			return payload;
		}

		var redacted = SecretShapeRegex().Replace(payload, Redaction);
		return redacted.Length > MaxPayloadLength
			? string.Concat(redacted.AsSpan(0, MaxPayloadLength), "…[truncated]")
			: redacted;
	}

	// Matches common secret shapes: "Bearer <token>", and long opaque key/token-like literals
	// (>=24 chars of base64url/hex). NonBacktracking guards against ReDoS on attacker-influenced input.
	[GeneratedRegex(
		@"(?i:bearer\s+[A-Za-z0-9._\-]+)|[A-Za-z0-9+/_\-]{24,}={0,2}",
		RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
	private static partial Regex SecretShapeRegex();
}
