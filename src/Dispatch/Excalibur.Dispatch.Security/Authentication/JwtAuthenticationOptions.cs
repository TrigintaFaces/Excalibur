// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Configuration options for JWT authentication.
/// </summary>
public sealed class JwtAuthenticationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether authentication is enabled.
	/// </summary>
	/// <value> <see langword="true" /> if authentication is enabled; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether authentication is required for all messages.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if authentication is required for all messages; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool RequireAuthentication { get; set; } = true;

	/// <summary>
	/// Gets or sets the context key where the token is stored.
	/// </summary>
	/// <value> The context key used to store the authentication token. The default is "AuthToken". </value>
	[Required]
	public string TokenContextKey { get; set; } = "AuthToken";

	/// <summary>
	/// Gets or sets the header name where the token is expected.
	/// </summary>
	/// <value> The header name where the authentication token is expected. The default is "Authorization". </value>
	public string TokenHeaderName { get; set; } = "Authorization";

	/// <summary>
	/// Gets or sets the property name to extract token from message.
	/// </summary>
	/// <value> The property name used to extract the authentication token from the message. The default is "AuthToken". </value>
	public string TokenPropertyName { get; set; } = "AuthToken";

	/// <summary>
	/// Gets or sets a value indicating whether to enable property extraction.
	/// </summary>
	/// <value> <see langword="true" /> if property extraction is enabled; otherwise, <see langword="false" />. The default is <see langword="false" />. </value>
	public bool EnablePropertyExtraction { get; set; }

	/// <summary>
	/// Gets or initializes message types that don't require authentication.
	/// </summary>
	/// <value> A set of message type names that are allowed to bypass authentication. The default is an empty set. </value>
	public ISet<string> AllowAnonymousMessageTypes { get; init; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets a value indicating whether to use async key retrieval.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if asynchronous key retrieval should be used; otherwise, <see langword="false" />. The default is <see langword="false" />.
	/// </value>
	public bool UseAsyncKeyRetrieval { get => Credentials.UseAsyncKeyRetrieval; set => Credentials.UseAsyncKeyRetrieval = value; }

	/// <summary>
	/// Gets or sets the credential name used to resolve the signing key from <see cref="ICredentialStore"/>.
	/// When set, the signing key is fetched from the registered <see cref="ICredentialStore"/> at runtime
	/// instead of using the static <see cref="SigningKey"/> property.
	/// </summary>
	/// <value>
	/// The credential name in the store, or <see langword="null"/> to use the static <see cref="SigningKey"/> property.
	/// </value>
	public string? SigningKeyCredentialName { get => Credentials.SigningKeyCredentialName; set => Credentials.SigningKeyCredentialName = value; }

	/// <summary>
	/// Gets or sets the clock skew in seconds.
	/// </summary>
	/// <value> The maximum allowed clock skew in seconds for token lifetime validation. The default is 300 (5 minutes). </value>
	[Range(0, int.MaxValue)]
	public int ClockSkewSeconds { get; set; } = 300; // 5 minutes

	/// <summary>
	/// Gets or sets token validation behavior options (issuer, audience, lifetime, signing key checks).
	/// </summary>
	/// <value> The token validation sub-options. </value>
	public JwtTokenValidationOptions Validation { get; set; } = new();

	/// <summary>
	/// Gets or sets token credential options (issuers, audiences, signing keys).
	/// </summary>
	/// <value> The token credential sub-options. </value>
	public JwtTokenCredentialOptions Credentials { get; set; } = new();

	// --- Backward-compatible shims that delegate to sub-options ---

	/// <summary>
	/// Gets or sets a value indicating whether to validate the issuer claim of the JWT token.
	/// </summary>
	/// <value> <see langword="true" /> if the issuer claim should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateIssuer { get => Validation.ValidateIssuer; set => Validation.ValidateIssuer = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to validate the audience claim of the JWT token.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if the audience claim should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool ValidateAudience { get => Validation.ValidateAudience; set => Validation.ValidateAudience = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to validate the lifetime of the JWT token.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if the token lifetime should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool ValidateLifetime { get => Validation.ValidateLifetime; set => Validation.ValidateLifetime = value; }

	/// <summary>
	/// Gets or sets a value indicating whether to validate the signing key of the JWT token.
	/// </summary>
	/// <value> <see langword="true" /> if the signing key should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateSigningKey { get => Validation.ValidateSigningKey; set => Validation.ValidateSigningKey = value; }

	/// <summary>
	/// Gets or sets a value indicating whether the JWT token must have an expiration time.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if the token must have an expiration time; otherwise, <see langword="false" />. The default is <see langword="true" />.
	/// </value>
	public bool RequireExpirationTime { get => Validation.RequireExpirationTime; set => Validation.RequireExpirationTime = value; }

	/// <summary>
	/// Gets or sets a value indicating whether the JWT token must be signed.
	/// </summary>
	/// <value> <see langword="true" /> if the token must be signed; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool RequireSignedTokens { get => Validation.RequireSignedTokens; set => Validation.RequireSignedTokens = value; }

	/// <summary>
	/// Gets or sets the valid issuer for JWT token validation.
	/// </summary>
	/// <value> The expected issuer value for JWT token validation, or <see langword="null" /> if any issuer is accepted. </value>
	public string? ValidIssuer { get => Credentials.ValidIssuer; set => Credentials.ValidIssuer = value; }

	/// <summary>
	/// Gets or sets the array of valid issuers for JWT token validation.
	/// </summary>
	/// <value> An array of expected issuer values for JWT token validation, or <see langword="null" /> if any issuer is accepted. </value>
	public string[]? ValidIssuers { get => Credentials.ValidIssuers; set => Credentials.ValidIssuers = value; }

	/// <summary>
	/// Gets or sets the valid audience for JWT token validation.
	/// </summary>
	/// <value> The expected audience value for JWT token validation, or <see langword="null" /> if any audience is accepted. </value>
	public string? ValidAudience { get => Credentials.ValidAudience; set => Credentials.ValidAudience = value; }

	/// <summary>
	/// Gets or sets the array of valid audiences for JWT token validation.
	/// </summary>
	/// <value> An array of expected audience values for JWT token validation, or <see langword="null" /> if any audience is accepted. </value>
	public string[]? ValidAudiences { get => Credentials.ValidAudiences; set => Credentials.ValidAudiences = value; }

	/// <summary>
	/// Gets or sets the signing key for symmetric algorithms.
	/// </summary>
	/// <value> The symmetric key used for token signing and validation, or <see langword="null" /> if asymmetric algorithms are used. </value>
	public string? SigningKey { get => Credentials.SigningKey; set => Credentials.SigningKey = value; }

	/// <summary>
	/// Gets or sets the RSA public key for asymmetric validation.
	/// </summary>
	/// <value> The RSA public key used for asymmetric token validation, or <see langword="null" /> if symmetric algorithms are used. </value>
	public string? RsaPublicKey { get => Credentials.RsaPublicKey; set => Credentials.RsaPublicKey = value; }
}

/// <summary>
/// Token validation behavior options for JWT authentication.
/// </summary>
public sealed class JwtTokenValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to validate the issuer claim.
	/// </summary>
	/// <value> <see langword="true" /> if the issuer claim should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateIssuer { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate the audience claim.
	/// </summary>
	/// <value> <see langword="true" /> if the audience claim should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateAudience { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate the lifetime.
	/// </summary>
	/// <value> <see langword="true" /> if the token lifetime should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateLifetime { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate the signing key.
	/// </summary>
	/// <value> <see langword="true" /> if the signing key should be validated; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool ValidateSigningKey { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the token must have an expiration time.
	/// </summary>
	/// <value> <see langword="true" /> if the token must have an expiration time; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool RequireExpirationTime { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the token must be signed.
	/// </summary>
	/// <value> <see langword="true" /> if the token must be signed; otherwise, <see langword="false" />. The default is <see langword="true" />. </value>
	public bool RequireSignedTokens { get; set; } = true;
}

/// <summary>
/// Token credential options for JWT authentication (issuers, audiences, keys).
/// </summary>
public sealed class JwtTokenCredentialOptions
{
	/// <summary>
	/// Gets or sets the valid issuer.
	/// </summary>
	/// <value> The expected issuer value, or <see langword="null" /> if any issuer is accepted. </value>
	public string? ValidIssuer { get; set; }

	/// <summary>
	/// Gets or sets the array of valid issuers.
	/// </summary>
	/// <value> An array of expected issuer values, or <see langword="null" /> if any issuer is accepted. </value>
	public string[]? ValidIssuers { get; set; }

	/// <summary>
	/// Gets or sets the valid audience.
	/// </summary>
	/// <value> The expected audience value, or <see langword="null" /> if any audience is accepted. </value>
	public string? ValidAudience { get; set; }

	/// <summary>
	/// Gets or sets the array of valid audiences.
	/// </summary>
	/// <value> An array of expected audience values, or <see langword="null" /> if any audience is accepted. </value>
	public string[]? ValidAudiences { get; set; }

	/// <summary>
	/// Gets or sets the signing key for symmetric algorithms.
	/// </summary>
	/// <value> The symmetric key used for token signing and validation, or <see langword="null" /> if asymmetric algorithms are used. </value>
	public string? SigningKey { get; set; }

	/// <summary>
	/// Gets or sets the RSA public key for asymmetric validation.
	/// </summary>
	/// <value> The RSA public key used for asymmetric token validation, or <see langword="null" /> if symmetric algorithms are used. </value>
	public string? RsaPublicKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use async key retrieval.
	/// </summary>
	/// <value>
	/// <see langword="true" /> if asynchronous key retrieval should be used; otherwise, <see langword="false" />. The default is <see langword="false" />.
	/// </value>
	public bool UseAsyncKeyRetrieval { get; set; }

	/// <summary>
	/// Gets or sets the credential name used to resolve the signing key from a credential store.
	/// When set, the signing key is fetched from the registered credential store at runtime
	/// instead of using the static <see cref="SigningKey"/> property.
	/// </summary>
	/// <value>
	/// The credential name in the store, or <see langword="null"/> to use the static <see cref="SigningKey"/> property.
	/// </value>
	public string? SigningKeyCredentialName { get; set; }
}
