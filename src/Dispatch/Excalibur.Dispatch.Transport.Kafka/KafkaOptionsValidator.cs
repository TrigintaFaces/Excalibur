// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates <see cref="KafkaOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class KafkaOptionsValidator : IValidateOptions<KafkaOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KafkaOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Kafka options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.BootstrapServers))
		{
			return ValidateOptionsResult.Fail(
				"Kafka BootstrapServers is required. Set KafkaOptions.BootstrapServers to a comma-separated list of broker addresses.");
		}

		if (options.Consumer is { MaxPayloadBytes: < 1 })
		{
			return ValidateOptionsResult.Fail(
				"KafkaOptions.Consumer.MaxPayloadBytes must be at least 1 byte when specified. Set it to null to opt out of the payload-size limit.");
		}

		return ValidateOptionsResult.Success;
	}
}
