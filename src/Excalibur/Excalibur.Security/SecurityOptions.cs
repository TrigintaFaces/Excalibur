// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Compliance;



namespace Excalibur.Security;

/// <summary>
/// Unified security configuration options.
/// </summary>
public sealed class SecurityOptions
{
	/// <summary>
	/// Gets or sets the encryption configuration options.
	/// </summary>
	/// <value>
	/// The encryption configuration options.
	/// </value>
	public SecurityEncryptionOptions Encryption { get; set; } = new();

	/// <summary>
	/// Gets or sets the message signing configuration options.
	/// </summary>
	/// <value>
	/// The message signing configuration options.
	/// </value>
	public SecuritySigningOptions Signing { get; set; } = new();

	/// <summary>
	/// Gets or sets the rate limiting configuration options.
	/// </summary>
	/// <value>
	/// The rate limiting configuration options.
	/// </value>
	public SecurityRateLimitOptions RateLimiting { get; set; } = new();

	/// <summary>
	/// Gets or sets the authentication configuration options.
	/// </summary>
	/// <value>
	/// The authentication configuration options.
	/// </value>
	public SecurityAuthenticationOptions Authentication { get; set; } = new();
}

/// <summary>
/// Encryption configuration options for security.
/// </summary>
public sealed class SecurityEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether message encryption is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if message encryption is enabled; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Every component on this type is off until it is named. The configuration delegate decides which
	/// parts of the security stack a host composes, so a default of <see langword="true"/> made naming
	/// any one component silently compose the others as well — and a host that asked only for encryption
	/// received JWT authentication it had no credentials for, leaving the dispatch pipeline unresolvable.
	/// Set this to <see langword="true"/> to compose message encryption.
	/// </remarks>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the encryption algorithm to use for message encryption.
	/// </summary>
	/// <value>
	/// The encryption algorithm to use for message encryption. The default is <see cref="EncryptionAlgorithm.Aes256Gcm"/>.
	/// </value>
	public EncryptionAlgorithm EncryptionAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

	/// <summary>
	/// Gets or sets the Azure Key Vault URL naming where encryption keys are held.
	/// </summary>
	/// <value>
	/// <see langword="null"/> - the only accepted value. This is forwarded to
	/// <see cref="EncryptionOptions.AzureKeyVaultUrl"/>, which refuses any other value at startup; see
	/// that property for why and for the supported way to hold keys in a vault.
	/// </value>
	public Uri? AzureKeyVaultUrl { get; set; }

	/// <summary>
	/// Gets or sets the AWS KMS key ARN naming where encryption keys are held.
	/// </summary>
	/// <value>
	/// <see langword="null"/> - the only accepted value. This is forwarded to
	/// <see cref="EncryptionOptions.AwsKmsKeyArn"/>, which refuses any other value at startup; see that
	/// property for why and for the supported way to hold keys in KMS.
	/// </value>
	public string? AwsKmsKeyArn { get; set; }
}

/// <summary>
/// Message signing configuration options for security.
/// </summary>
public sealed class SecuritySigningOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether message signing is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if message signing is enabled; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Signing is opt-in because it is the one security component that cannot run on the framework's own
	/// resources: it needs an <see cref="IKeyProvider"/> supplying key material shared by every process
	/// that signs or verifies, which is a deployment decision only the consumer can make. Turning it on by
	/// default handed signing — and that infrastructure requirement — to consumers who asked only for
	/// encryption or rate limiting. Set this to <see langword="true"/> and register a key provider
	/// together; enabling it without one fails loudly at host startup.
	/// </remarks>
	public bool EnableSigning { get; set; }

	/// <summary>
	/// Gets or sets the signing algorithm to use for message signatures.
	/// </summary>
	/// <value>
	/// The signing algorithm to use for message signatures. The default is <see cref="SigningAlgorithm.HMACSHA256"/>.
	/// </value>
	public SigningAlgorithm SigningAlgorithm { get; set; } = SigningAlgorithm.HMACSHA256;
}

/// <summary>
/// Rate limiting configuration options for security.
/// </summary>
public sealed class SecurityRateLimitOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether rate limiting is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if rate limiting is enabled; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Off until named, for the reason given on
	/// <see cref="SecurityEncryptionOptions.EnableEncryption"/>. Set this to <see langword="true"/> to
	/// compose rate limiting.
	/// </remarks>
	public bool EnableRateLimiting { get; set; }

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
}

/// <summary>
/// Authentication configuration options for security.
/// </summary>
public sealed class SecurityAuthenticationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether authentication is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if authentication is enabled; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// Off until named, for the reason given on
	/// <see cref="SecurityEncryptionOptions.EnableEncryption"/>. Authentication is the component that
	/// showed the cost most plainly: composing it requires an issuer, an audience and a signing key, so a
	/// host that never asked for it could not build its container. Set this to <see langword="true"/>,
	/// together with the JWT settings below, to compose authentication.
	/// </remarks>
	public bool EnableAuthentication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether authentication is required for all requests.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if authentication is required for all requests; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// This one stays on, and the difference from the <c>Enable</c> flags above is the point. Those decide
	/// whether a component is composed at all, and composing something nobody named is a surprise in
	/// either direction. This decides how authentication behaves once a host has asked for it, and a host
	/// that asks for authentication and then does not enforce it has bought nothing. Set this to
	/// <see langword="false"/> to authenticate opportunistically and authorize elsewhere.
	/// </remarks>
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
}
