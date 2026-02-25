// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance;



namespace Excalibur.Dispatch.Security;

/// <summary>
/// Unified security configuration options.
/// </summary>
public sealed class SecurityOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether message encryption is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if message encryption is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableEncryption { get; set; } = true;

	/// <summary>
	/// Gets or sets the encryption algorithm to use for message encryption.
	/// </summary>
	/// <value>
	/// The encryption algorithm to use for message encryption. The default is <see cref="EncryptionAlgorithm.Aes256Gcm"/>.
	/// </value>
	public EncryptionAlgorithm EncryptionAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets the Azure Key Vault URL for encryption key management.
	/// </summary>
	/// <value>
	/// The Azure Key Vault URL for encryption key management, or <see langword="null"/> if not configured.
	/// </value>
	public Uri? AzureKeyVaultUrl { get; set; }

	/// <summary>
	/// Gets or sets the AWS KMS key ARN for encryption key management.
	/// </summary>
	/// <value>
	/// The AWS KMS key ARN for encryption key management, or <see langword="null"/> if not configured.
	/// </value>
	public string? AwsKmsKeyArn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether message signing is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if message signing is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableSigning { get; set; } = true;

	/// <summary>
	/// Gets or sets the signing algorithm to use for message signatures.
	/// </summary>
	/// <value>
	/// The signing algorithm to use for message signatures. The default is <see cref="SigningAlgorithm.HMACSHA256"/>.
	/// </value>
	public SigningAlgorithm SigningAlgorithm { get; set; } = SigningAlgorithm.HMACSHA256;

	/// <summary>
	/// Gets or sets a value indicating whether rate limiting is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if rate limiting is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableRateLimiting { get; set; } = true;

	/// <summary>
	/// Gets or sets the rate limiting algorithm to use.
	/// </summary>
	/// <value>
	/// The rate limiting algorithm to use. The default is <see cref="RateLimitAlgorithm.TokenBucket"/>.
	/// </value>
	public RateLimitAlgorithm RateLimitAlgorithm { get; set; } = RateLimitAlgorithm.TokenBucket;

	/// <summary>
	/// Gets or sets the default rate limiting parameters.
	/// </summary>
	/// <value>
	/// The default rate limiting parameters to apply when no specific limits are configured.
	/// </value>
	public RateLimits DefaultRateLimits { get; set; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether authentication is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if authentication is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableAuthentication { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether authentication is required for all requests.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if authentication is required for all requests; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool RequireAuthentication { get; set; } = true;

	/// <summary>
	/// Gets or sets the JWT token issuer for authentication validation.
	/// </summary>
	/// <value>
	/// The JWT token issuer for authentication validation, or <see langword="null"/> if not configured.
	/// </value>
	public string? JwtIssuer { get; set; }

	/// <summary>
	/// Gets or sets the JWT token audience for authentication validation.
	/// </summary>
	/// <value>
	/// The JWT token audience for authentication validation, or <see langword="null"/> if not configured.
	/// </value>
	public string? JwtAudience { get; set; }

	/// <summary>
	/// Gets or sets the JWT token signing key for authentication validation.
	/// </summary>
	/// <value>
	/// The JWT token signing key for authentication validation, or <see langword="null"/> if not configured.
	/// </value>
	public string? JwtSigningKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether security headers should be added to responses.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if security headers should be added to responses; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableSecurityHeaders { get; set; } = true;

	/// <summary>
	/// Gets or initializes custom security headers to add to responses.
	/// </summary>
	/// <value>
	/// A dictionary of custom security headers to add to responses, or an empty dictionary if no custom headers are configured.
	/// </value>
	public IDictionary<string, string> CustomHeaders { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
