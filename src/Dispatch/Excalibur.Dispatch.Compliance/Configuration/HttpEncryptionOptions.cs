// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Configuration options for HTTP encryption behaviors.
/// </summary>
/// <remarks>
/// <para>
/// These options configure encryption behavior for HTTP request/response handling,
/// including automatic field encryption, header encryption, and cookie protection.
/// </para>
/// <para>
/// Use in conjunction with ASP.NET Core middleware for automatic encryption
/// of sensitive data in HTTP traffic.
/// </para>
/// </remarks>
public sealed class HttpEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether HTTP encryption is enabled.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets options for request body encryption.
	/// </summary>
	public HttpRequestEncryptionOptions Request { get; set; } = new();

	/// <summary>
	/// Gets or sets options for response body encryption.
	/// </summary>
	public HttpResponseEncryptionOptions Response { get; set; } = new();

	/// <summary>
	/// Gets or sets options for cookie encryption.
	/// </summary>
	public HttpCookieEncryptionOptions Cookies { get; set; } = new();

	/// <summary>
	/// Gets or sets options for header encryption.
	/// </summary>
	public HttpHeaderEncryptionOptions Headers { get; set; } = new();

	/// <summary>
	/// Gets or sets the paths that should be excluded from encryption.
	/// </summary>
	/// <remarks>
	/// Paths are matched using glob patterns. Examples:
	/// - "/health" - exact match
	/// - "/api/public/*" - wildcard match
	/// - "/swagger/**" - recursive match
	/// </remarks>
	public List<string> ExcludedPaths { get; set; } =
	[
		"/health",
		"/healthz",
		"/ready",
		"/readyz",
		"/live",
		"/livez",
		"/metrics",
		"/swagger*",
	];

	/// <summary>
	/// Gets or sets a value indicating whether to log encryption operations.
	/// </summary>
	/// <value>Default is <c>true</c> for development, <c>false</c> for production.</value>
	public bool EnableLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets the provider ID to use for HTTP encryption.
	/// </summary>
	/// <remarks>
	/// When <c>null</c>, the primary encryption provider is used.
	/// </remarks>
	public string? ProviderId { get; set; }
}

/// <summary>
/// Configuration options for request body encryption.
/// </summary>
public sealed class HttpRequestEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to decrypt encrypted request bodies.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool EnableDecryption { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate request integrity.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool ValidateIntegrity { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum allowed age for encrypted request data.
	/// </summary>
	/// <remarks>
	/// Requests with data encrypted longer ago than this threshold are rejected.
	/// Helps prevent replay attacks. Set to <c>null</c> to disable.
	/// </remarks>
	public TimeSpan? MaxDataAge { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets content types that should be processed.
	/// </summary>
	public List<string> SupportedContentTypes { get; set; } =
	[
		"application/json",
		"application/x-www-form-urlencoded",
	];
}

/// <summary>
/// Configuration options for response body encryption.
/// </summary>
public sealed class HttpResponseEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to encrypt response bodies.
	/// </summary>
	/// <value>Default is <c>false</c> - encryption must be explicitly enabled.</value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the fields in JSON responses that should be encrypted.
	/// </summary>
	/// <remarks>
	/// Uses JSON path notation. Examples:
	/// - "$.password" - encrypt password field at root
	/// - "$.user.ssn" - encrypt nested ssn field
	/// - "$..creditCard" - encrypt creditCard anywhere in response
	/// </remarks>
	public List<string> EncryptedFields { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to include encryption metadata.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool IncludeMetadata { get; set; } = true;

	/// <summary>
	/// Gets or sets content types that should be processed.
	/// </summary>
	public List<string> SupportedContentTypes { get; set; } =
	[
		"application/json",
	];
}

/// <summary>
/// Configuration options for cookie encryption.
/// </summary>
public sealed class HttpCookieEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether cookie encryption is enabled.
	/// </summary>
	/// <value>Default is <c>true</c>.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the cookie names that should be encrypted.
	/// </summary>
	/// <remarks>
	/// Supports wildcards. Examples:
	/// - "session" - exact match
	/// - "auth_*" - prefix match
	/// - "*_token" - suffix match
	/// </remarks>
	public List<string> EncryptedCookies { get; set; } =
	[
		"session",
		"auth*",
		"*_token",
		"refresh_token",
	];

	/// <summary>
	/// Gets or sets cookie names that should never be encrypted.
	/// </summary>
	public List<string> ExcludedCookies { get; set; } =
	[
		"consent",
		"locale",
		"theme",
	];

	/// <summary>
	/// Gets or sets a value indicating whether to use authenticated encryption for cookies.
	/// </summary>
	/// <value>Default is <c>true</c> - adds integrity protection.</value>
	public bool AuthenticatedEncryption { get; set; } = true;
}

/// <summary>
/// Configuration options for header encryption.
/// </summary>
public sealed class HttpHeaderEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether header encryption is enabled.
	/// </summary>
	/// <value>Default is <c>false</c> - must be explicitly enabled.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the header names that should be encrypted.
	/// </summary>
	/// <remarks>
	/// Header values are encrypted, names remain readable.
	/// Use with caution as encrypted headers may cause issues with proxies.
	/// </remarks>
	public List<string> EncryptedHeaders { get; set; } =
	[
		"X-Api-Key",
		"X-Auth-Token",
	];

	/// <summary>
	/// Gets or sets the prefix used for encrypted header values.
	/// </summary>
	/// <value>Default is "enc:".</value>
	public string EncryptedValuePrefix { get; set; } = "enc:";
}

/// <summary>
/// Configuration options for encryption behavior in API endpoints.
/// </summary>
public sealed class ApiEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether API encryption is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the base paths for APIs that should use encryption.
	/// </summary>
	public List<string> ApiPaths { get; set; } =
	[
		"/api",
	];

	/// <summary>
	/// Gets or sets a value indicating whether to require encrypted payloads for sensitive endpoints.
	/// </summary>
	public bool RequireEncryptedPayloads { get; set; }

	/// <summary>
	/// Gets or sets endpoints that require encrypted payloads.
	/// </summary>
	public List<string> EncryptionRequiredEndpoints { get; set; } = [];

	/// <summary>
	/// Gets or sets the HTTP header used to indicate encrypted content.
	/// </summary>
	public string EncryptionIndicatorHeader { get; set; } = "X-Content-Encrypted";

	/// <summary>
	/// Gets or sets the HTTP header used to specify the encryption key ID.
	/// </summary>
	public string KeyIdHeader { get; set; } = "X-Encryption-Key-Id";

	/// <summary>
	/// Gets or sets the HTTP header used to specify the encryption provider.
	/// </summary>
	public string ProviderHeader { get; set; } = "X-Encryption-Provider";
}
