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

		if (options.ValidateIssuer &&
			string.IsNullOrWhiteSpace(options.ValidIssuer) &&
			(options.ValidIssuers is null || options.ValidIssuers.Length == 0))
		{
			failures.Add(
				$"When {nameof(JwtAuthenticationOptions.ValidateIssuer)} is true, " +
				$"at least one of {nameof(JwtAuthenticationOptions.ValidIssuer)} or " +
				$"{nameof(JwtAuthenticationOptions.ValidIssuers)} must be set.");
		}

		if (options.ValidateAudience &&
			string.IsNullOrWhiteSpace(options.ValidAudience) &&
			(options.ValidAudiences is null || options.ValidAudiences.Length == 0))
		{
			failures.Add(
				$"When {nameof(JwtAuthenticationOptions.ValidateAudience)} is true, " +
				$"at least one of {nameof(JwtAuthenticationOptions.ValidAudience)} or " +
				$"{nameof(JwtAuthenticationOptions.ValidAudiences)} must be set.");
		}

		if (options.ValidateSigningKey &&
			string.IsNullOrWhiteSpace(options.SigningKey) &&
			string.IsNullOrWhiteSpace(options.RsaPublicKey) &&
			string.IsNullOrWhiteSpace(options.SigningKeyCredentialName))
		{
			failures.Add(
				$"When {nameof(JwtAuthenticationOptions.ValidateSigningKey)} is true, " +
				$"at least one of {nameof(JwtAuthenticationOptions.SigningKey)}, " +
				$"{nameof(JwtAuthenticationOptions.RsaPublicKey)}, or " +
				$"{nameof(JwtAuthenticationOptions.SigningKeyCredentialName)} must be set.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
