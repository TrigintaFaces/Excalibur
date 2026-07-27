// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>Validates <see cref="AzureProviderOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AzureProviderOptionsValidator : IValidateOptions<AzureProviderOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AzureProviderOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxMessageSizeBytes < 1)
		{
			failures.Add($"{nameof(AzureProviderOptions.MaxMessageSizeBytes)} must be greater than zero.");
		}

		if (options.PrefetchCount < 0)
		{
			failures.Add($"{nameof(AzureProviderOptions.PrefetchCount)} must not be negative.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
