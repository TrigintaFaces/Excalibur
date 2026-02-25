// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Telemetry;

/// <summary>
/// A no-op implementation of <see cref="ITelemetrySanitizer"/> that passes all values through unchanged.
/// </summary>
/// <remarks>
/// Use this implementation when PII sanitization is not required, such as in
/// development environments or when raw telemetry data is acceptable.
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
