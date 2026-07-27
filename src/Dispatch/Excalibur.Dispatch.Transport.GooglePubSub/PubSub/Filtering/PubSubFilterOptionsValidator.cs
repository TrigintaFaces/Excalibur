// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>Validates <see cref="PubSubFilterOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class PubSubFilterOptionsValidator : IValidateOptions<PubSubFilterOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, PubSubFilterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.Enabled && string.IsNullOrWhiteSpace(options.FilterExpression)
			? ValidateOptionsResult.Fail(
				$"{nameof(PubSubFilterOptions.FilterExpression)} must be a non-empty expression when " +
				$"{nameof(PubSubFilterOptions.Enabled)} is true.")
			: ValidateOptionsResult.Success;
	}
}
