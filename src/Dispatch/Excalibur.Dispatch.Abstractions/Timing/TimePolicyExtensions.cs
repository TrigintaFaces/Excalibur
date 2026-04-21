// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for <see cref="ITimePolicy"/>.
/// </summary>
public static class TimePolicyExtensions
{
	/// <summary>Gets the timeout for serialization operations.</summary>
	public static TimeSpan SerializationTimeout(this ITimePolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		if (policy is ITimePolicyConfiguration config)
		{
			return config.SerializationTimeout;
		}
		return policy.GetTimeoutFor(TimeoutOperationType.Serialization);
	}

	/// <summary>Gets the timeout for transport operations.</summary>
	public static TimeSpan TransportTimeout(this ITimePolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		if (policy is ITimePolicyConfiguration config)
		{
			return config.TransportTimeout;
		}
		return policy.GetTimeoutFor(TimeoutOperationType.Transport);
	}

	/// <summary>Gets the timeout for validation operations.</summary>
	public static TimeSpan ValidationTimeout(this ITimePolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		if (policy is ITimePolicyConfiguration config)
		{
			return config.ValidationTimeout;
		}
		return policy.GetTimeoutFor(TimeoutOperationType.Validation);
	}

	/// <summary>Determines if a timeout should be applied based on the operation context.</summary>
	public static bool ShouldApplyTimeout(this ITimePolicy policy, TimeoutOperationType operationType, TimeoutContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(policy);
		if (policy is ITimePolicyConfiguration config)
		{
			return config.ShouldApplyTimeout(operationType, context);
		}
		return true;
	}
}
