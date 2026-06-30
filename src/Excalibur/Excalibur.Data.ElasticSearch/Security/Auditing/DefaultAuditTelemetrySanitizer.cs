// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Telemetry;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Self-contained default <see cref="ITelemetrySanitizer"/> for the Elasticsearch security-audit sink, so
/// PII masking works zero-config (secure-by-default) without pulling core <c>Excalibur.Dispatch</c> or
/// <c>Excalibur.Compliance</c> into this package.
/// </summary>
/// <remarks>
/// <para>
/// Tag values (IP address, user agent, etc.) are replaced with a stable, non-reversible SHA-256
/// fingerprint — they stay correlatable across records (the same IP hashes identically) without the raw
/// value ever reaching the long-retention index. Free-form payloads (failure reasons, context) have
/// secret-shaped substrings redacted and are length-capped.
/// </para>
/// <para>
/// Consumers needing a richer sanitizer (e.g. <c>HashingTelemetrySanitizer</c> via
/// <c>AddDispatchObservability()</c>) override this via a non-<c>Try</c> registration, which wins over the
/// <c>TryAddSingleton</c> default.
/// </para>
/// </remarks>
internal sealed partial class DefaultAuditTelemetrySanitizer : ITelemetrySanitizer
{
	private const int MaxPayloadLength = 4096;
	private const string Redaction = "***REDACTED***";

	/// <inheritdoc />
	/// <remarks>
	/// Returns a stable, non-reversible <c>sha256:</c> fingerprint of the raw value so distinct values stay
	/// distinguishable for correlation while the raw PII never lands in the index. A null/empty value is
	/// passed through unchanged (nothing sensitive to mask).
	/// </remarks>
	public string? SanitizeTag(string tagName, string? rawValue)
	{
		if (string.IsNullOrEmpty(rawValue))
		{
			return rawValue;
		}

		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

		// 12 bytes (96 bits) of the digest keeps collisions negligible while staying compact. Audit masking
		// is not a hot path, so a straightforward allocation is preferred over premature span micro-tuning.
		return string.Concat("sha256:", Convert.ToHexStringLower(hash.AsSpan(0, 12)));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Redacts secret-shaped substrings (bearer tokens, long opaque key/token literals) and caps the length
	/// so an oversized or credential-bearing free-form field cannot land raw in the index.
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
