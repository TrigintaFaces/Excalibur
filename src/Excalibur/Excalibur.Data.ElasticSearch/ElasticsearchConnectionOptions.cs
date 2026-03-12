// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures connection-level settings for Elasticsearch client connections, including authentication,
/// timeouts, SSL/TLS, and connection pool tuning.
/// </summary>
public sealed class ElasticsearchConnectionOptions
{
	/// <summary>
	/// Gets the certificate fingerprint for SSL/TLS verification.
	/// </summary>
	/// <value> A <see cref="string" /> representing the certificate fingerprint for secure connections, or <c> null </c> if not required. </value>
	public string? CertificateFingerprint { get; init; }

	/// <summary>
	/// Gets the username for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the username, or <c> null </c> if basic authentication is not used. </value>
	public string? Username { get; init; }

	/// <summary>
	/// Gets the password for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the password, or <c> null </c> if basic authentication is not used. </value>
	public string? Password { get; init; }

	/// <summary>
	/// Gets the API key for Elasticsearch authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the API key, or <c> null </c> if API key authentication is not used. </value>
	public string? ApiKey { get; init; }

	/// <summary>
	/// Gets the Base64-encoded API key for Elasticsearch authentication.
	/// </summary>
	/// <value>
	/// A <see cref="string" /> representing the Base64-encoded API key, or <c> null </c> if Base64 API key authentication is not used.
	/// </value>
	public string? Base64ApiKey { get; init; }

	/// <summary>
	/// Gets the request timeout for Elasticsearch operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the request timeout. Defaults to 30 seconds. </value>
	public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the ping timeout for node health checks.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the ping timeout. Defaults to 5 seconds. </value>
	public TimeSpan PingTimeout { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets the maximum number of connections per node.
	/// </summary>
	/// <value> An <see cref="int" /> representing the maximum connections per node. Defaults to 80. </value>
	public int MaximumConnectionsPerNode { get; init; } = 80;

	/// <summary>
	/// Gets a value indicating whether to disable SSL certificate validation.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to disable SSL certificate validation. Defaults to <c> false </c>. </value>
	public bool DisableCertificateValidation { get; init; }

	/// <summary>
	/// Gets the interval for sniffing node information.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the sniffing interval. Defaults to 1 hour. </value>
	public TimeSpan SniffingInterval { get; init; } = TimeSpan.FromHours(1);
}
