// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

		if (options.MaxPayloadBytes is <= 0)
		{
			failures.Add($"{nameof(MqttOptions.MaxPayloadBytes)}, when set, must be positive.");
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
