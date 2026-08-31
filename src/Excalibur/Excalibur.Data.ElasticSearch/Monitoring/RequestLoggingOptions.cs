// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures request and response logging for Elasticsearch operations.
/// </summary>
public sealed class RequestLoggingOptions
{
	/// <summary>
	/// Gets a value indicating whether request/response logging is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to log requests and responses. Defaults to <c> false </c>. </value>
	public bool Enabled { get; init; }

	/// <summary>
	/// Gets a value indicating whether to log request bodies.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include request body content. Defaults to <c> false </c>. </value>
	/// <remarks>
	/// Body values are redacted unless their property name appears in <see cref="AllowedBodyProperties" />, so enabling
	/// this without an allow list records the shape of each body and none of its content.
	/// </remarks>
	public bool LogRequestBody { get; init; }

	/// <summary>
	/// Gets a value indicating whether to log response bodies.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include response body content. Defaults to <c> false </c>. </value>
	/// <remarks>
	/// Body values are redacted unless their property name appears in <see cref="AllowedBodyProperties" />, so enabling
	/// this without an allow list records the shape of each body and none of its content.
	/// </remarks>
	public bool LogResponseBody { get; init; }

	/// <summary>
	/// Gets the body property names whose values may be written to the log verbatim.
	/// </summary>
	/// <value>
	/// The set of property names treated as safe to record. Empty by default, which redacts every body value. Names are
	/// matched without regard to case.
	/// </value>
	/// <remarks>
	/// <para>
	/// This is an allow list, not a deny list: a property that does not appear here is redacted, so a field nobody
	/// anticipated is withheld rather than recorded. Naming a property here opts its value out of redaction wherever it
	/// occurs in the body, including nested objects and arrays, so name only properties that carry no personal data,
	/// credentials, or other sensitive content.
	/// </para>
	/// <para>
	/// Property <em> names </em> are preserved even when their values are redacted, because a log stripped of structure
	/// is of little diagnostic use. A body whose property names are themselves sensitive - a map keyed by email address,
	/// for example - is therefore not a candidate for body logging at all.
	/// </para>
	/// <para>
	/// A body that is not well-formed JSON cannot be redacted one value at a time and is withheld in full.
	/// </para>
	/// </remarks>
	public ISet<string> AllowedBodyProperties { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets a value indicating whether to log only failed requests.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to log only failed operations. Defaults to <c> true </c>. </value>
	public bool LogFailuresOnly { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to log the transport layer's diagnostic dump for failed operations.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include transport debug information. Defaults to <c> false </c>. </value>
	/// <remarks>
	/// The transport composes this text itself and it commonly embeds the full request and response bodies. It cannot be
	/// redacted structurally and <see cref="AllowedBodyProperties" /> does not apply to it, so enabling this records
	/// request and response content in the clear. Failures are already logged with their status code, request URI, and
	/// error reason without it.
	/// </remarks>
	public bool LogTransportDebugInformation { get; init; }

	/// <summary>
	/// Gets the maximum body size to log in bytes.
	/// </summary>
	/// <value> The maximum size of request/response bodies to log. Defaults to 1024 bytes. </value>
	/// <remarks>
	/// The limit applies to the redacted body, so it bounds what actually reaches the log. It must be greater than
	/// zero; to record no body content, leave <see cref="LogRequestBody" /> and <see cref="LogResponseBody" /> off
	/// rather than setting a size of zero.
	/// </remarks>
	public int MaxBodySizeBytes { get; init; } = 1024;
}
