// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Aws;

/// <summary>Validates <see cref="AwsSchedulerOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AwsSchedulerOptionsValidator : IValidateOptions<AwsSchedulerOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AwsSchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.TargetArn))
		{
			failures.Add($"{nameof(AwsSchedulerOptions.TargetArn)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ExecutionRoleArn))
		{
			failures.Add($"{nameof(AwsSchedulerOptions.ExecutionRoleArn)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TimeZone))
		{
			failures.Add($"{nameof(AwsSchedulerOptions.TimeZone)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ScheduleGroup))
		{
			failures.Add($"{nameof(AwsSchedulerOptions.ScheduleGroup)} is required.");
		}

		if (options.MaximumEventAgeInSeconds < 1)
		{
			failures.Add($"{nameof(AwsSchedulerOptions.MaximumEventAgeInSeconds)} must be greater than zero.");
		}

		if (options.RetryPolicyMaximumRetryAttempts < 0)
		{
			failures.Add($"{nameof(AwsSchedulerOptions.RetryPolicyMaximumRetryAttempts)} must not be negative.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
