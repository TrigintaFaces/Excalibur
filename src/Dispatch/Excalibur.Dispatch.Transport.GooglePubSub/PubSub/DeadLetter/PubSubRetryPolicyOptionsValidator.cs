// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>Validates <see cref="PubSubRetryPolicyOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class PubSubRetryPolicyOptionsValidator : IValidateOptions<PubSubRetryPolicyOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PubSubRetryPolicyOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.CustomStrategies.Any(kvp => string.IsNullOrWhiteSpace(kvp.Key))
			? ValidateOptionsResult.Fail(
				$"{nameof(PubSubRetryPolicyOptions.CustomStrategies)} must not contain a null or empty strategy key.")
			: ValidateOptionsResult.Success;
	}
}
