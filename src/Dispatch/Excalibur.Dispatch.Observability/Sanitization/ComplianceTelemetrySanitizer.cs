// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions.Telemetry;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Sanitizes telemetry data for regulatory compliance (GDPR, CCPA, HIPAA) by detecting
/// and redacting or hashing PII patterns in tag values and payloads.
/// </summary>
/// <remarks>
/// <para>
/// This sanitizer applies two layers of protection:
/// </para>
/// <list type="number">
/// <item><strong>Tag-name-based redaction</strong> — Known PII tag names are redacted or hashed unconditionally.</item>
/// <item><strong>Pattern-based detection</strong> — Regex patterns detect embedded PII (emails, phone numbers, SSNs)
/// in both tag values and payloads, replacing matches with the configured placeholder or hash.</item>
/// </list>
/// <para>
/// The sanitizer delegates to an inner <see cref="ITelemetrySanitizer"/> (typically
/// <see cref="HashingTelemetrySanitizer"/>) for baseline sanitization, then applies
/// compliance-specific rules on top. This layered approach means compliance sanitization
/// is additive and does not bypass existing protections.
/// </para>
/// <para>
/// Hash results are cached using a bounded <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// (capacity 1024) to avoid repeated computation for frequently seen values.
/// </para>
/// </remarks>
public sealed partial class ComplianceTelemetrySanitizer : ITelemetrySanitizer
{
	/// <summary>
	/// Maximum number of entries in the hash cache. When full, new values skip caching.
	/// </summary>
	private const int MaxCacheSize = 1024;

	private readonly ITelemetrySanitizer _inner;
	private readonly bool _enabled;
	private readonly bool _hashDetectedPii;
	private readonly string _redactedPlaceholder;
	private readonly HashSet<string> _redactedTagNames;
	private readonly Regex[] _customPatterns;
	private readonly bool _detectEmails;
	private readonly bool _detectPhoneNumbers;
	private readonly bool _detectSsns;
	private readonly ConcurrentDictionary<string, string> _hashCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="ComplianceTelemetrySanitizer"/> class.
	/// </summary>
	/// <param name="options">The compliance sanitizer configuration options.</param>
	/// <param name="sanitizerOptions">The base telemetry sanitizer options used by the inner sanitizer.</param>
	public ComplianceTelemetrySanitizer(
		IOptions<ComplianceTelemetrySanitizerOptions> options,
		IOptions<TelemetrySanitizerOptions> sanitizerOptions)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(sanitizerOptions);

		var opts = options.Value;
		_enabled = opts.Enabled;
		_hashDetectedPii = opts.HashDetectedPii;
		_redactedPlaceholder = opts.RedactedPlaceholder;
		_redactedTagNames = new HashSet<string>(opts.RedactedTagNames, StringComparer.OrdinalIgnoreCase);
		_detectEmails = opts.DetectEmails;
		_detectPhoneNumbers = opts.DetectPhoneNumbers;
		_detectSsns = opts.DetectSocialSecurityNumbers;

		_customPatterns = BuildCustomPatterns(opts.CustomPatterns);

		// Create the inner sanitizer for baseline protection
		_inner = new HashingTelemetrySanitizer(sanitizerOptions);
	}

	/// <inheritdoc />
	public string? SanitizeTag(string tagName, string? rawValue)
	{
		// Always apply baseline sanitization first
		var baseResult = _inner.SanitizeTag(tagName, rawValue);

		if (!_enabled || baseResult is null)
		{
			return baseResult;
		}

		// Check compliance-specific tag name redaction
		if (_redactedTagNames.Contains(tagName))
		{
			return _hashDetectedPii ? HashValue(rawValue ?? string.Empty) : _redactedPlaceholder;
		}

		// Apply pattern-based PII detection on the (already baseline-sanitized) value
		return ApplyPatternDetection(baseResult);
	}

	/// <inheritdoc />
	public string SanitizePayload(string payload)
	{
		// Always apply baseline sanitization first
		var baseResult = _inner.SanitizePayload(payload);

		if (!_enabled)
		{
			return baseResult;
		}

		// Apply pattern-based PII detection on the payload
		return ApplyPatternDetection(baseResult);
	}

	private string ApplyPatternDetection(string value)
	{
		var result = value;

		if (_detectEmails)
		{
			result = ReplacePattern(EmailPattern(), result);
		}

		if (_detectPhoneNumbers)
		{
			result = ReplacePattern(PhonePattern(), result);
		}

		if (_detectSsns)
		{
			result = ReplacePattern(SsnPattern(), result);
		}

		for (var i = 0; i < _customPatterns.Length; i++)
		{
			result = ReplacePattern(_customPatterns[i], result);
		}

		return result;
	}

	private string ReplacePattern(Regex pattern, string input)
	{
		if (_hashDetectedPii)
		{
			return pattern.Replace(input, match => HashValue(match.Value));
		}

		return pattern.Replace(input, _redactedPlaceholder);
	}

	private string HashValue(string value)
	{
		if (_hashCache.TryGetValue(value, out var cached))
		{
			return cached;
		}

		var hash = ComputeSha256Hash(value);

		// Bounded cache: skip caching when full to prevent unbounded growth
		if (_hashCache.Count < MaxCacheSize)
		{
			_hashCache.TryAdd(value, hash);
		}

		return hash;
	}

	private static string ComputeSha256Hash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return string.Create(71, bytes, static (span, hash) =>
		{
			"sha256:".AsSpan().CopyTo(span);
			for (var i = 0; i < hash.Length; i++)
			{
				var b = hash[i];
				span[7 + (i * 2)] = ToLowerHexChar(b >> 4);
				span[7 + (i * 2) + 1] = ToLowerHexChar(b & 0x0F);
			}
		});
	}

	private static char ToLowerHexChar(int value) =>
		(char)(value < 10 ? '0' + value : 'a' + value - 10);

	private static Regex[] BuildCustomPatterns(IList<string> patterns)
	{
		if (patterns.Count == 0)
		{
			return [];
		}

		var result = new Regex[patterns.Count];
		for (var i = 0; i < patterns.Count; i++)
		{
			result[i] = new Regex(patterns[i], RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
		}

		return result;
	}

	/// <summary>
	/// Matches email addresses (e.g., user@example.com).
	/// </summary>
	[GeneratedRegex(
		@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
		RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
		matchTimeoutMilliseconds: 1000)]
	private static partial Regex EmailPattern();

	/// <summary>
	/// Matches phone numbers in common formats (international and US).
	/// Examples: +1-555-123-4567, (555) 123-4567, 555.123.4567, +44 20 7946 0958.
	/// </summary>
	[GeneratedRegex(
		@"(?<!\d)(\+?\d{1,3}[\s\-.]?)?\(?\d{3}\)?[\s\-.]?\d{3}[\s\-.]?\d{4}(?!\d)",
		RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
		matchTimeoutMilliseconds: 1000)]
	private static partial Regex PhonePattern();

	/// <summary>
	/// Matches US Social Security Numbers (e.g., 123-45-6789 or 123456789).
	/// </summary>
	[GeneratedRegex(
		@"(?<!\d)\d{3}[\-\s]?\d{2}[\-\s]?\d{4}(?!\d)",
		RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
		matchTimeoutMilliseconds: 1000)]
	private static partial Regex SsnPattern();
}
