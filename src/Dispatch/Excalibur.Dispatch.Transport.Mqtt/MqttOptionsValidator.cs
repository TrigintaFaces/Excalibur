// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Mqtt;

/// <summary>
/// Validates <see cref="MqttOptions"/> at startup (fail-fast) so a misconfigured MQTT transport is rejected
/// before the first publish/subscribe rather than surfacing as a runtime connection failure.
/// </summary>
internal sealed class MqttOptionsValidator : IValidateOptions<MqttOptions>
{
	public ValidateOptionsResult Validate(string? name, MqttOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Host))
		{
			failures.Add($"{nameof(MqttOptions.Host)} is required.");
		}

		if (options.Port is < 1 or > 65535)
		{
			failures.Add($"{nameof(MqttOptions.Port)} must be in the range 1..65535.");
		}

		if (string.IsNullOrWhiteSpace(options.ClientId))
		{
			failures.Add($"{nameof(MqttOptions.ClientId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Topic))
		{
			failures.Add($"{nameof(MqttOptions.Topic)} is required.");
		}

		if (!Enum.IsDefined(options.QualityOfService))
		{
			failures.Add($"{nameof(MqttOptions.QualityOfService)} must be a defined QoS level (0, 1, or 2).");
		}
		else if (options.QualityOfService == MqttQualityOfService.AtMostOnce)
		{
			// QoS 0 IS REFUSED RATHER THAN ACCEPTED AND LIED ABOUT.
			// At this level the protocol sends no acknowledgement packet, so every operation on the
			// settlement surface silently does nothing while reporting success: acknowledging settles a
			// message the broker stopped tracking the moment it sent it; rejecting with requeue cannot cause
			// a redelivery, so it discards the message outright; and rejecting without requeue suppresses a
			// redelivery that was never going to happen. The rejection case is the one that costs a
			// consumer real work -- it reads as "this will come back" and is in fact a silent drop.
			//
			// This is refusable here because it is a property of the CONFIGURATION rather than of a
			// particular call, so the bad state is made unreachable instead of merely detected later.
			failures.Add(
				$"{nameof(MqttOptions.QualityOfService)} must be {nameof(MqttQualityOfService.AtLeastOnce)} (1) or "
				+ $"{nameof(MqttQualityOfService.ExactlyOnce)} (2) for a receiver: under "
				+ $"{nameof(MqttQualityOfService.AtMostOnce)} (0) MQTT sends no acknowledgement packet, so "
				+ "acknowledging and rejecting a message both do nothing while reporting success, and a "
				+ "rejected message is discarded rather than redelivered. Publish-only hosts that do not "
				+ "receive are unaffected by this requirement.");
		}

		if (options.MaxPayloadBytes is <= 0)
		{
			failures.Add($"{nameof(MqttOptions.MaxPayloadBytes)}, when set, must be positive.");
		}

		// The session expiry is refused rather than coerced. It is encoded on the wire as a whole number of
		// seconds, so a fractional or out-of-range window cannot be represented -- and silently rounding it would
		// hand the consumer a recovery window different from the one they configured, which is the same class of
		// defect as the library default this setting exists to correct.
		if (options.PersistentSession)
		{
			if (options.SessionExpiryInterval <= TimeSpan.Zero)
			{
				failures.Add(
					$"{nameof(MqttOptions.SessionExpiryInterval)} must be positive when {nameof(MqttOptions.PersistentSession)} "
					+ "is enabled: a session that expires immediately is discarded when the connection closes, so a rejected "
					+ "message is never redelivered.");
			}
			else if (options.SessionExpiryInterval.Ticks % TimeSpan.TicksPerSecond != 0)
			{
				failures.Add(
					$"{nameof(MqttOptions.SessionExpiryInterval)} must be a whole number of seconds; "
					+ $"{options.SessionExpiryInterval} cannot be represented on the wire.");
			}
			else if (options.SessionExpiryInterval.TotalSeconds > uint.MaxValue)
			{
				failures.Add(
					$"{nameof(MqttOptions.SessionExpiryInterval)} must not exceed {uint.MaxValue} seconds.");
			}
		}

		else
		{
			// A NON-PERSISTENT SESSION REMOVES THE ONLY MECHANISM BY WHICH A REQUEUE CAN BE HONOURED.
			// Rejecting with requeue:true withholds the acknowledgement and relies on the broker resuming
			// the session to redeliver. Under a clean start the broker discards the session with the
			// connection, so the withheld message is gone -- the caller asked for redelivery and got a
			// silent discard. Refused for the same reason as QoS 0 above, and by the same test: it is
			// decidable from configuration alone.
			failures.Add(
				$"{nameof(MqttOptions.PersistentSession)} must be enabled for a receiver: with a clean start the "
				+ "broker discards the session when the connection closes, so a message rejected with "
				+ "requeue is never redelivered and is silently lost instead.");
		}

		if (options.UseSharedSubscription && string.IsNullOrWhiteSpace(options.SharedSubscriptionGroup))
		{
			failures.Add($"{nameof(MqttOptions.SharedSubscriptionGroup)} is required when {nameof(MqttOptions.UseSharedSubscription)} is enabled.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
