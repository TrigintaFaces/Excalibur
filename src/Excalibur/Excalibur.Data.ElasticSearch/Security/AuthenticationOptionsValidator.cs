// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Validates <see cref="AuthenticationOptions"/> at startup. Reflection-free (AOT-safe).
/// Enforces partial-credential completeness (a configured API-key credential must be complete),
/// without rejecting valid single-method configurations.
/// </summary>
internal sealed class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// API-key credentials are both-or-neither: an ApiKeyId without its Base64ApiKey (or vice-versa)
		// is an incomplete, silently-broken API-key configuration.
		var hasApiKeyId = !string.IsNullOrWhiteSpace(options.ApiKeyId);
		var hasBase64ApiKey = !string.IsNullOrWhiteSpace(options.Base64ApiKey);

		return hasApiKeyId != hasBase64ApiKey
			? ValidateOptionsResult.Fail(
				$"{nameof(AuthenticationOptions.ApiKeyId)} and {nameof(AuthenticationOptions.Base64ApiKey)} must be " +
				"both specified or both omitted (an incomplete API-key credential is invalid).")
			: ValidateOptionsResult.Success;
	}
}
