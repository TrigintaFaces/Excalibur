// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Validates <see cref="AwsEventBridgeOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AwsEventBridgeOptionsValidator : IValidateOptions<AwsEventBridgeOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsEventBridgeOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.EventBusName))
		{
			failures.Add($"{nameof(AwsEventBridgeOptions.EventBusName)} must be a non-empty event bus name.");
		}

		if (string.IsNullOrWhiteSpace(options.DefaultSource))
		{
			failures.Add($"{nameof(AwsEventBridgeOptions.DefaultSource)} must be a non-empty source.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
