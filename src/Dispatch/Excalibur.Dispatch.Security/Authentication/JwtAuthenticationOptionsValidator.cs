// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Validates <see cref="JwtAuthenticationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
internal sealed class JwtAuthenticationOptionsValidator : IValidateOptions<JwtAuthenticationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, JwtAuthenticationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		var failures = new List<string>();

		if (options.ClockSkewSeconds < 0)
		{
			failures.Add($"{nameof(JwtAuthenticationOptions.ClockSkewSeconds)} must be >= 0 (was {options.ClockSkewSeconds}).");
		}

		if (options.Validation.ValidateIssuer &&
			string.IsNullOrWhiteSpace(options.Credentials.ValidIssuer) &&
			(options.Credentials.ValidIssuers is null || options.Credentials.ValidIssuers.Length == 0))
		{
			failures.Add(
				$"When {nameof(JwtTokenValidationOptions)}.{nameof(JwtTokenValidationOptions.ValidateIssuer)} is true, " +
				$"at least one of {nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.ValidIssuer)} or " +
				$"{nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.ValidIssuers)} must be set.");
		}

		if (options.Validation.ValidateAudience &&
			string.IsNullOrWhiteSpace(options.Credentials.ValidAudience) &&
			(options.Credentials.ValidAudiences is null || options.Credentials.ValidAudiences.Length == 0))
		{
			failures.Add(
				$"When {nameof(JwtTokenValidationOptions)}.{nameof(JwtTokenValidationOptions.ValidateAudience)} is true, " +
				$"at least one of {nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.ValidAudience)} or " +
				$"{nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.ValidAudiences)} must be set.");
		}

		if (options.Validation.ValidateSigningKey &&
			string.IsNullOrWhiteSpace(options.Credentials.SigningKey) &&
			string.IsNullOrWhiteSpace(options.Credentials.RsaPublicKey) &&
			string.IsNullOrWhiteSpace(options.Credentials.SigningKeyCredentialName))
		{
			failures.Add(
				$"When {nameof(JwtTokenValidationOptions)}.{nameof(JwtTokenValidationOptions.ValidateSigningKey)} is true, " +
				$"at least one of {nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.SigningKey)}, " +
				$"{nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.RsaPublicKey)}, or " +
				$"{nameof(JwtTokenCredentialOptions)}.{nameof(JwtTokenCredentialOptions.SigningKeyCredentialName)} must be set.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
