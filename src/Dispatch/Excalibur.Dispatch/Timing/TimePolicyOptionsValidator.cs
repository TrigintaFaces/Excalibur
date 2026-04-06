// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Timing;

/// <summary>
/// Validates <see cref="TimePolicyOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks and AOT-safe TimeSpan range validation including:
/// <list type="bullet">
///   <item><description>DefaultTimeout must be between 1 second and 1 hour</description></item>
///   <item><description>MaxTimeout must be between 30 seconds and 1 hour</description></item>
///   <item><description>DefaultTimeout must be less than MaxTimeout</description></item>
///   <item><description>HandlerTimeout must be between 5 seconds and 30 minutes</description></item>
///   <item><description>SerializationTimeout must be between 1 second and 5 minutes</description></item>
///   <item><description>TransportTimeout must be between 5 seconds and 10 minutes</description></item>
///   <item><description>ValidationTimeout must be between 1 second and 1 minute</description></item>
///   <item><description>HandlerTimeout must not exceed MaxTimeout</description></item>
///   <item><description>TransportTimeout must not exceed MaxTimeout</description></item>
///   <item><description>SerializationTimeout must be less than HandlerTimeout</description></item>
///   <item><description>ValidationTimeout must be less than HandlerTimeout</description></item>
///   <item><description>Custom timeouts must not exceed MaxTimeout</description></item>
///   <item><description>ComplexityMultiplier must be less than or equal to HeavyOperationMultiplier</description></item>
/// </list>
/// </remarks>
internal sealed class TimePolicyOptionsValidator : IValidateOptions<TimePolicyOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TimePolicyOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// AOT-safe TimeSpan range checks (replaces [Range(typeof(TimeSpan), ...)] attributes)
		if (options.DefaultTimeout < TimeSpan.FromSeconds(1) || options.DefaultTimeout > TimeSpan.FromHours(1))
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.DefaultTimeout)} must be between 00:00:01 and 01:00:00 (was {options.DefaultTimeout}).");
		}

		if (options.MaxTimeout < TimeSpan.FromSeconds(30) || options.MaxTimeout > TimeSpan.FromHours(1))
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.MaxTimeout)} must be between 00:00:30 and 01:00:00 (was {options.MaxTimeout}).");
		}

		if (options.OperationTimeouts.HandlerTimeout < TimeSpan.FromSeconds(5) || options.OperationTimeouts.HandlerTimeout > TimeSpan.FromMinutes(30))
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.HandlerTimeout)} must be between 00:00:05 and 00:30:00 (was {options.OperationTimeouts.HandlerTimeout}).");
		}

		if (options.OperationTimeouts.SerializationTimeout < TimeSpan.FromSeconds(1) || options.OperationTimeouts.SerializationTimeout > TimeSpan.FromMinutes(5))
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.SerializationTimeout)} must be between 00:00:01 and 00:05:00 (was {options.OperationTimeouts.SerializationTimeout}).");
		}

		if (options.OperationTimeouts.TransportTimeout < TimeSpan.FromSeconds(5) || options.OperationTimeouts.TransportTimeout > TimeSpan.FromMinutes(10))
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.TransportTimeout)} must be between 00:00:05 and 00:10:00 (was {options.OperationTimeouts.TransportTimeout}).");
		}

		if (options.OperationTimeouts.ValidationTimeout < TimeSpan.FromSeconds(1) || options.OperationTimeouts.ValidationTimeout > TimeSpan.FromMinutes(1))
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.ValidationTimeout)} must be between 00:00:01 and 00:01:00 (was {options.OperationTimeouts.ValidationTimeout}).");
		}

		// Cross-property checks
		// DefaultTimeout must be less than MaxTimeout
		if (options.DefaultTimeout >= options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.DefaultTimeout)} ({options.DefaultTimeout}) " +
				$"must be less than {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// HandlerTimeout must not exceed MaxTimeout
		if (options.OperationTimeouts.HandlerTimeout > options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.HandlerTimeout)} ({options.OperationTimeouts.HandlerTimeout}) " +
				$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// TransportTimeout must not exceed MaxTimeout
		if (options.OperationTimeouts.TransportTimeout > options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.TransportTimeout)} ({options.OperationTimeouts.TransportTimeout}) " +
				$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// SerializationTimeout must be less than HandlerTimeout
		if (options.OperationTimeouts.SerializationTimeout >= options.OperationTimeouts.HandlerTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.SerializationTimeout)} ({options.OperationTimeouts.SerializationTimeout}) " +
				$"must be less than {nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.HandlerTimeout)} ({options.OperationTimeouts.HandlerTimeout}).");
		}

		// ValidationTimeout must be less than HandlerTimeout
		if (options.OperationTimeouts.ValidationTimeout >= options.OperationTimeouts.HandlerTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.ValidationTimeout)} ({options.OperationTimeouts.ValidationTimeout}) " +
				$"must be less than {nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.HandlerTimeout)} ({options.OperationTimeouts.HandlerTimeout}).");
		}

		// ComplexityMultiplier should be <= HeavyOperationMultiplier
		if (options.OperationTimeouts.ComplexityMultiplier > options.OperationTimeouts.HeavyOperationMultiplier)
		{
			failures.Add(
				$"{nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.ComplexityMultiplier)} ({options.OperationTimeouts.ComplexityMultiplier}) " +
				$"must be less than or equal to {nameof(TimePolicyOperationTimeoutOptions)}.{nameof(TimePolicyOperationTimeoutOptions.HeavyOperationMultiplier)} ({options.OperationTimeouts.HeavyOperationMultiplier}).");
		}

		// Custom timeouts must not exceed MaxTimeout
		foreach (var customTimeout in options.Overrides.CustomTimeouts)
		{
			if (customTimeout.Value > options.MaxTimeout)
			{
				failures.Add(
					$"Custom timeout for {customTimeout.Key} ({customTimeout.Value}) " +
					$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
			}
		}

		foreach (var messageTimeout in options.Overrides.MessageTypeTimeouts)
		{
			if (messageTimeout.Value > options.MaxTimeout)
			{
				failures.Add(
					$"Message type timeout for '{messageTimeout.Key}' ({messageTimeout.Value}) " +
					$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
			}
		}

		foreach (var handlerTimeout in options.Overrides.HandlerTypeTimeouts)
		{
			if (handlerTimeout.Value > options.MaxTimeout)
			{
				failures.Add(
					$"Handler type timeout for '{handlerTimeout.Key}' ({handlerTimeout.Value}) " +
					$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
