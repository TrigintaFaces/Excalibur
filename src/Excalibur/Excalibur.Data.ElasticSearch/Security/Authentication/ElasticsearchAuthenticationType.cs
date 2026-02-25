// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Specifies the supported Elasticsearch authentication methods with security characteristics.
/// </summary>
public enum ElasticsearchAuthenticationType
{
	/// <summary>
	/// No authentication configured. Should only be used in development environments.
	/// </summary>
	None = 0,

	/// <summary>
	/// Basic username and password authentication with HTTPS transport encryption.
	/// </summary>
	BasicAuthentication = 1,

	/// <summary>
	/// Elasticsearch API key authentication with optional key rotation support.
	/// </summary>
	ApiKey = 2,

	/// <summary>
	/// Base64-encoded API key authentication for legacy integration scenarios.
	/// </summary>
	Base64ApiKey = 3,

	/// <summary>
	/// Certificate-based mutual TLS authentication for maximum security.
	/// </summary>
	CertificateAuthentication = 4,

	/// <summary>
	/// OAuth 2.0 / OpenID Connect authentication with token refresh support.
	/// </summary>
	OAuth2 = 5,

	/// <summary>
	/// SAML-based authentication for enterprise single sign-on integration.
	/// </summary>
	Saml = 6,

	/// <summary>
	/// Service account authentication for machine-to-machine communication.
	/// </summary>
	ServiceAccount = 7,
}
