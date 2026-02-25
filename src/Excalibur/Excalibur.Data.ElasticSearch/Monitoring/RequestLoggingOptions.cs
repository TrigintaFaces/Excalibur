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
	public bool LogRequestBody { get; init; }

	/// <summary>
	/// Gets a value indicating whether to log response bodies.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to include response body content. Defaults to <c> false </c>. </value>
	public bool LogResponseBody { get; init; }

	/// <summary>
	/// Gets a value indicating whether to log only failed requests.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to log only failed operations. Defaults to <c> true </c>. </value>
	public bool LogFailuresOnly { get; init; } = true;

	/// <summary>
	/// Gets the maximum body size to log in bytes.
	/// </summary>
	/// <value> The maximum size of request/response bodies to log. Defaults to 1024 bytes. </value>
	public int MaxBodySizeBytes { get; init; } = 1024;

	/// <summary>
	/// Gets a value indicating whether to sanitize sensitive data in logs.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to mask sensitive information. Defaults to <c> true </c>. </value>
	public bool SanitizeSensitiveData { get; init; } = true;
}
