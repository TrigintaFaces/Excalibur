// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Timing;

/// <summary>
/// Validates <see cref="TimePolicyOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>DefaultTimeout must be less than MaxTimeout</description></item>
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

		// DefaultTimeout must be less than MaxTimeout
		if (options.DefaultTimeout >= options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.DefaultTimeout)} ({options.DefaultTimeout}) " +
				$"must be less than {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// HandlerTimeout must not exceed MaxTimeout
		if (options.HandlerTimeout > options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.HandlerTimeout)} ({options.HandlerTimeout}) " +
				$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// TransportTimeout must not exceed MaxTimeout
		if (options.TransportTimeout > options.MaxTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.TransportTimeout)} ({options.TransportTimeout}) " +
				$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
		}

		// SerializationTimeout must be less than HandlerTimeout
		if (options.SerializationTimeout >= options.HandlerTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.SerializationTimeout)} ({options.SerializationTimeout}) " +
				$"must be less than {nameof(TimePolicyOptions.HandlerTimeout)} ({options.HandlerTimeout}).");
		}

		// ValidationTimeout must be less than HandlerTimeout
		if (options.ValidationTimeout >= options.HandlerTimeout)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.ValidationTimeout)} ({options.ValidationTimeout}) " +
				$"must be less than {nameof(TimePolicyOptions.HandlerTimeout)} ({options.HandlerTimeout}).");
		}

		// ComplexityMultiplier should be <= HeavyOperationMultiplier
		if (options.ComplexityMultiplier > options.HeavyOperationMultiplier)
		{
			failures.Add(
				$"{nameof(TimePolicyOptions.ComplexityMultiplier)} ({options.ComplexityMultiplier}) " +
				$"must be less than or equal to {nameof(TimePolicyOptions.HeavyOperationMultiplier)} ({options.HeavyOperationMultiplier}).");
		}

		// Custom timeouts must not exceed MaxTimeout
		foreach (var customTimeout in options.CustomTimeouts)
		{
			if (customTimeout.Value > options.MaxTimeout)
			{
				failures.Add(
					$"Custom timeout for {customTimeout.Key} ({customTimeout.Value}) " +
					$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
			}
		}

		foreach (var messageTimeout in options.MessageTypeTimeouts)
		{
			if (messageTimeout.Value > options.MaxTimeout)
			{
				failures.Add(
					$"Message type timeout for '{messageTimeout.Key}' ({messageTimeout.Value}) " +
					$"must not exceed {nameof(TimePolicyOptions.MaxTimeout)} ({options.MaxTimeout}).");
			}
		}

		foreach (var handlerTimeout in options.HandlerTypeTimeouts)
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
