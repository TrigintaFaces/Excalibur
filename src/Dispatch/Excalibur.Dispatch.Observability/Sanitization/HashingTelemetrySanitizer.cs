// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions.Telemetry;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Sanitizes telemetry data by hashing sensitive values using SHA-256.
/// </summary>
/// <remarks>
/// <para>
/// Tag values are classified into three categories based on configuration:
/// </para>
/// <list type="bullet">
/// <item><strong>Sensitive</strong> — hashed using SHA-256, emitted as <c>sha256:&lt;hex&gt;</c></item>
/// <item><strong>Suppressed</strong> — tag is suppressed entirely (returns <see langword="null"/>)</item>
/// <item><strong>Passthrough</strong> — returned unchanged</item>
/// </list>
/// <para>
/// Hash results are cached using a bounded <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// (capacity 1024) to avoid repeated computation for frequently seen values.
/// </para>
/// </remarks>
public sealed class HashingTelemetrySanitizer : ITelemetrySanitizer
{
	/// <summary>
	/// Maximum number of entries in the hash cache. When full, new values skip caching.
	/// </summary>
	private const int MaxCacheSize = 1024;

	private readonly HashSet<string> _sensitiveTagNames;
	private readonly HashSet<string> _suppressedTagNames;
	private readonly bool _includeRawPii;
	private readonly ConcurrentDictionary<string, string> _hashCache = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="HashingTelemetrySanitizer"/> class.
	/// </summary>
	/// <param name="options">The sanitizer configuration options.</param>
	public HashingTelemetrySanitizer(IOptions<TelemetrySanitizerOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var opts = options.Value;
		_includeRawPii = opts.IncludeRawPii;
		_sensitiveTagNames = new HashSet<string>(opts.SensitiveTagNames, StringComparer.OrdinalIgnoreCase);
		_suppressedTagNames = new HashSet<string>(opts.SuppressedTagNames, StringComparer.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public string? SanitizeTag(string tagName, string? rawValue)
	{
		if (_includeRawPii)
		{
			return rawValue;
		}

		if (_suppressedTagNames.Contains(tagName))
		{
			return null;
		}

		if (_sensitiveTagNames.Contains(tagName) && rawValue is not null)
		{
			return HashValue(rawValue);
		}

		return rawValue;
	}

	/// <inheritdoc />
	public string SanitizePayload(string payload)
	{
		if (_includeRawPii)
		{
			return payload;
		}

		return HashValue(payload);
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
}
