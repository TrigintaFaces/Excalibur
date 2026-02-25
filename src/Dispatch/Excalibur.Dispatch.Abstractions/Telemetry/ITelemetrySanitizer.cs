// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Telemetry;

/// <summary>
/// Sanitizes telemetry data to prevent PII leakage into observability systems.
/// </summary>
/// <remarks>
/// <para>
/// Implementations control how sensitive data (user IDs, email addresses, tokens)
/// is handled before being emitted as span tags, log properties, or metric dimensions.
/// </para>
/// <para>
/// The default implementation (<c>HashingTelemetrySanitizer</c>) uses SHA-256 hashing
/// for sensitive values, suppresses highly sensitive tags entirely, and passes through
/// non-sensitive data unchanged.
/// </para>
/// </remarks>
public interface ITelemetrySanitizer
{
	/// <summary>
	/// Sanitizes a telemetry tag value before it is emitted.
	/// </summary>
	/// <param name="tagName">The name of the tag (e.g., "user.id", "auth.email").</param>
	/// <param name="rawValue">The raw tag value to sanitize.</param>
	/// <returns>
	/// The sanitized value, or <see langword="null"/> to suppress the tag entirely.
	/// </returns>
	string? SanitizeTag(string tagName, string? rawValue);

	/// <summary>
	/// Sanitizes a payload string (e.g., a message body or log message) before it is emitted.
	/// </summary>
	/// <param name="payload">The raw payload to sanitize.</param>
	/// <returns>The sanitized payload.</returns>
	string SanitizePayload(string payload);
}
