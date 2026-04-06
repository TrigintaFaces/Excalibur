// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Validates <see cref="TimeAwareSchedulerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class TimeAwareSchedulerOptionsValidator : IValidateOptions<TimeAwareSchedulerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TimeAwareSchedulerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// Root-level range checks
		if (options.HeavyOperationMultiplier is < 1.0 or > 5.0)
		{
			failures.Add($"{nameof(TimeAwareSchedulerOptions.HeavyOperationMultiplier)} must be between 1.0 and 5.0 (was {options.HeavyOperationMultiplier}).");
		}

		if (options.ComplexOperationMultiplier is < 1.0 or > 3.0)
		{
			failures.Add($"{nameof(TimeAwareSchedulerOptions.ComplexOperationMultiplier)} must be between 1.0 and 3.0 (was {options.ComplexOperationMultiplier}).");
		}

		// SchedulerTimeoutOptions range checks
		var minScheduleRetrieval = TimeSpan.FromSeconds(5);
		var maxScheduleRetrieval = TimeSpan.FromMinutes(5);
		if (options.Timeouts.ScheduleRetrievalTimeout < minScheduleRetrieval || options.Timeouts.ScheduleRetrievalTimeout > maxScheduleRetrieval)
		{
			failures.Add($"Timeouts.{nameof(SchedulerTimeoutOptions.ScheduleRetrievalTimeout)} must be between {minScheduleRetrieval} and {maxScheduleRetrieval} (was {options.Timeouts.ScheduleRetrievalTimeout}).");
		}

		var minDeserialization = TimeSpan.FromSeconds(1);
		var maxDeserialization = TimeSpan.FromMinutes(1);
		if (options.Timeouts.DeserializationTimeout < minDeserialization || options.Timeouts.DeserializationTimeout > maxDeserialization)
		{
			failures.Add($"Timeouts.{nameof(SchedulerTimeoutOptions.DeserializationTimeout)} must be between {minDeserialization} and {maxDeserialization} (was {options.Timeouts.DeserializationTimeout}).");
		}

		var minDispatch = TimeSpan.FromSeconds(30);
		var maxDispatch = TimeSpan.FromMinutes(10);
		if (options.Timeouts.DispatchTimeout < minDispatch || options.Timeouts.DispatchTimeout > maxDispatch)
		{
			failures.Add($"Timeouts.{nameof(SchedulerTimeoutOptions.DispatchTimeout)} must be between {minDispatch} and {maxDispatch} (was {options.Timeouts.DispatchTimeout}).");
		}

		var minScheduleUpdate = TimeSpan.FromSeconds(5);
		var maxScheduleUpdate = TimeSpan.FromMinutes(2);
		if (options.Timeouts.ScheduleUpdateTimeout < minScheduleUpdate || options.Timeouts.ScheduleUpdateTimeout > maxScheduleUpdate)
		{
			failures.Add($"Timeouts.{nameof(SchedulerTimeoutOptions.ScheduleUpdateTimeout)} must be between {minScheduleUpdate} and {maxScheduleUpdate} (was {options.Timeouts.ScheduleUpdateTimeout}).");
		}

		var minMaxScheduling = TimeSpan.FromMinutes(1);
		var maxMaxScheduling = TimeSpan.FromMinutes(15);
		if (options.Timeouts.MaxSchedulingTimeout < minMaxScheduling || options.Timeouts.MaxSchedulingTimeout > maxMaxScheduling)
		{
			failures.Add($"Timeouts.{nameof(SchedulerTimeoutOptions.MaxSchedulingTimeout)} must be between {minMaxScheduling} and {maxMaxScheduling} (was {options.Timeouts.MaxSchedulingTimeout}).");
		}

		// SchedulerAdaptiveOptions range checks
		if (options.Adaptive.AdaptiveTimeoutPercentile is < 75 or > 99)
		{
			failures.Add($"Adaptive.{nameof(SchedulerAdaptiveOptions.AdaptiveTimeoutPercentile)} must be between 75 and 99 (was {options.Adaptive.AdaptiveTimeoutPercentile}).");
		}

		if (options.Adaptive.MinimumSampleSize is < 10 or > 1000)
		{
			failures.Add($"Adaptive.{nameof(SchedulerAdaptiveOptions.MinimumSampleSize)} must be between 10 and 1000 (was {options.Adaptive.MinimumSampleSize}).");
		}

		if (options.Adaptive.TimeoutEscalationMultiplier is < 1.1 or > 3.0)
		{
			failures.Add($"Adaptive.{nameof(SchedulerAdaptiveOptions.TimeoutEscalationMultiplier)} must be between 1.1 and 3.0 (was {options.Adaptive.TimeoutEscalationMultiplier}).");
		}

		if (options.Adaptive.MaxTimeoutEscalations is < 1 or > 10)
		{
			failures.Add($"Adaptive.{nameof(SchedulerAdaptiveOptions.MaxTimeoutEscalations)} must be between 1 and 10 (was {options.Adaptive.MaxTimeoutEscalations}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
