// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Telemetry;

/// <summary>
/// A no-op implementation of <see cref="ITelemetrySanitizer"/> that passes all values through unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <b>WARNING: This implementation provides NO PII protection.</b> All tag values and payloads
/// are passed through to telemetry backends without sanitization. This is the default when
/// <c>AddDispatchObservability()</c> has not been called.
/// </para>
/// <para>
/// For production use, call <c>AddDispatchObservability()</c> which registers
/// <c>HashingTelemetrySanitizer</c> (SHA-256 hashing of PII fields). Use this implementation
/// only in development environments or when raw telemetry data is explicitly acceptable.
/// </para>
/// </remarks>
public sealed class NullTelemetrySanitizer : ITelemetrySanitizer
{
	/// <summary>
	/// The shared singleton instance.
	/// </summary>
	public static readonly NullTelemetrySanitizer Instance = new();

	private NullTelemetrySanitizer()
	{
	}

	/// <inheritdoc />
	public string? SanitizeTag(string tagName, string? rawValue) => rawValue;

	/// <inheritdoc />
	public string SanitizePayload(string payload) => payload;
}
