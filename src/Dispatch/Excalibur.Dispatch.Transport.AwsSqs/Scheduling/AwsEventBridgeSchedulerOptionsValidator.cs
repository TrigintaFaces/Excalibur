// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Validates <see cref="AwsEventBridgeSchedulerOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AwsEventBridgeSchedulerOptionsValidator : IValidateOptions<AwsEventBridgeSchedulerOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsEventBridgeSchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Region))
		{
			failures.Add($"{nameof(AwsEventBridgeSchedulerOptions.Region)} must be a non-empty AWS region.");
		}

		if (string.IsNullOrWhiteSpace(options.ScheduleGroupName))
		{
			failures.Add($"{nameof(AwsEventBridgeSchedulerOptions.ScheduleGroupName)} must be a non-empty schedule group.");
		}

		if (string.IsNullOrWhiteSpace(options.ScheduleTimeZone))
		{
			failures.Add($"{nameof(AwsEventBridgeSchedulerOptions.ScheduleTimeZone)} must be a non-empty time zone.");
		}

		if (options.MaxRetryAttempts < 0)
		{
			failures.Add($"{nameof(AwsEventBridgeSchedulerOptions.MaxRetryAttempts)} must not be negative.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
