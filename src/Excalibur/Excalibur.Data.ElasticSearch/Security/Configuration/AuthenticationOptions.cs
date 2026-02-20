// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures authentication methods and credential management for Elasticsearch connections.
/// </summary>
public sealed class AuthenticationOptions
{
	/// <summary>
	/// Gets the username for basic authentication.
	/// </summary>
	/// <value> The username for HTTP basic authentication, or null if not used. </value>
	public string? Username { get; init; }

	/// <summary>
	/// Gets the API key identifier for API key authentication.
	/// </summary>
	/// <value> The API key ID for Elasticsearch API key authentication, or null if not used. </value>
	public string? ApiKeyId { get; init; }

	/// <summary>
	/// Gets the Base64-encoded API key for legacy authentication scenarios.
	/// </summary>
	/// <value> The complete Base64-encoded API key, or null if not used. </value>
	public string? Base64ApiKey { get; init; }

	/// <summary>
	/// Gets the certificate-based authentication configuration.
	/// </summary>
	/// <value> Settings for mutual TLS certificate authentication. </value>
	public CertificateAuthenticationOptions Certificate { get; init; } = new();

	/// <summary>
	/// Gets the OAuth2 authentication configuration.
	/// </summary>
	/// <value> Settings for OAuth2/OpenID Connect authentication. </value>
	public OAuth2Options OAuth2 { get; init; } = new();

	/// <summary>
	/// Gets the service account authentication configuration.
	/// </summary>
	/// <value> Settings for service account-based authentication. </value>
	public ServiceAccountOptions ServiceAccount { get; init; } = new();

	/// <summary>
	/// Gets the credential rotation configuration.
	/// </summary>
	/// <value> Settings for automatic credential rotation and lifecycle management. </value>
	public CredentialRotationOptions CredentialRotation { get; init; } = new();

	/// <summary>
	/// Gets the authentication failure protection configuration.
	/// </summary>
	/// <value> Settings for rate limiting and account protection against authentication attacks. </value>
	public AuthenticationProtectionOptions Protection { get; init; } = new();
}
