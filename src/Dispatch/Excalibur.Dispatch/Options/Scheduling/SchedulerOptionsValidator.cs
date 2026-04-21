// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Validates <see cref="SchedulerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class SchedulerOptionsValidator : IValidateOptions<SchedulerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PollInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SchedulerOptions.PollInterval)} must be greater than zero (was {options.PollInterval}).");
		}

		if (options.MinPollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(SchedulerOptions.MinPollingInterval)} must be greater than zero (was {options.MinPollingInterval}).");
		}

		if (options.EnableAdaptivePolling && options.MinPollingInterval >= options.PollInterval)
		{
			failures.Add($"{nameof(SchedulerOptions.MinPollingInterval)} ({options.MinPollingInterval}) must be less than {nameof(SchedulerOptions.PollInterval)} ({options.PollInterval}) when adaptive polling is enabled.");
		}

		if (options.AdaptivePollingBackoffMultiplier < 1.0)
		{
			failures.Add($"{nameof(SchedulerOptions.AdaptivePollingBackoffMultiplier)} must be >= 1.0 (was {options.AdaptivePollingBackoffMultiplier}).");
		}

		if (options.PollingJitterRatio is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(SchedulerOptions.PollingJitterRatio)} must be between 0.0 and 1.0 (was {options.PollingJitterRatio}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
