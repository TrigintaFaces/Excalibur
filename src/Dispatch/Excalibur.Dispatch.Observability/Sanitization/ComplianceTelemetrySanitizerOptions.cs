// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Observability.Sanitization;

/// <summary>
/// Configuration options for <see cref="ComplianceTelemetrySanitizer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Controls compliance-level sanitization of telemetry data by combining tag-name-based
/// rules (inherited from <see cref="TelemetrySanitizerOptions"/>) with regex-based
/// pattern detection for PII embedded in tag values and payloads.
/// </para>
/// <para>
/// Default patterns detect common PII formats: email addresses, phone numbers (international
/// and US formats), and US Social Security Numbers. Additional custom patterns can be added
/// via <see cref="CustomPatterns"/>.
/// </para>
/// </remarks>
public sealed class ComplianceTelemetrySanitizerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether compliance sanitization is enabled.
	/// </summary>
	/// <value><see langword="true"/> to enable compliance sanitization; <see langword="true"/> by default.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the replacement string used when PII patterns are detected in payloads.
	/// </summary>
	/// <value>The replacement string. Defaults to <c>[REDACTED]</c>.</value>
	[Required]
	public string RedactedPlaceholder { get; set; } = "[REDACTED]";

	/// <summary>
	/// Gets or sets a value indicating whether detected PII in tag values should be
	/// hashed (SHA-256) instead of redacted.
	/// </summary>
	/// <value><see langword="true"/> to hash detected PII; <see langword="false"/> to replace with <see cref="RedactedPlaceholder"/>. Defaults to <see langword="false"/>.</value>
	public bool HashDetectedPii { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether email pattern detection is enabled.
	/// </summary>
	/// <value><see langword="true"/> to detect email addresses; <see langword="true"/> by default.</value>
	public bool DetectEmails { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether phone number pattern detection is enabled.
	/// </summary>
	/// <value><see langword="true"/> to detect phone numbers; <see langword="true"/> by default.</value>
	public bool DetectPhoneNumbers { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether US Social Security Number pattern detection is enabled.
	/// </summary>
	/// <value><see langword="true"/> to detect SSNs; <see langword="true"/> by default.</value>
	public bool DetectSocialSecurityNumbers { get; set; } = true;

	/// <summary>
	/// Gets or sets additional tag names whose values should always be redacted,
	/// beyond the defaults in <see cref="TelemetrySanitizerOptions"/>.
	/// </summary>
	/// <value>A list of additional tag names to redact. Defaults to common PII tag names.</value>
	[Required]
	public IList<string> RedactedTagNames { get; set; } =
	[
		"user.email",
		"user.phone",
		"user.ssn",
		"user.ip_address",
		"user.address",
		"http.client_ip",
		"enduser.id",
		"enduser.email",
	];

	/// <summary>
	/// Gets or sets custom regex patterns for detecting PII in tag values and payloads.
	/// Each pattern is applied in addition to the built-in detectors.
	/// </summary>
	/// <value>A list of regex pattern strings. Empty by default.</value>
	public IList<string> CustomPatterns { get; set; } = [];
}
