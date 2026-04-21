// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="CloudWatchMetricsOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class CloudWatchMetricsOptionsValidator : IValidateOptions<CloudWatchMetricsOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CloudWatchMetricsOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("CloudWatch metrics options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.Namespace))
		{
			return ValidateOptionsResult.Fail(
				"CloudWatch Namespace is required. Set CloudWatchMetricsOptions.Namespace to the target CloudWatch namespace (e.g., 'MyApp/Dispatch').");
		}

		return ValidateOptionsResult.Success;
	}
}
