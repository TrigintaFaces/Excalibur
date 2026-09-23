// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

		if (options.Consumer is { EnableAutoCommit: true })
		{
			// Refused at startup rather than degraded at runtime. Background auto-commit publishes a position
			// on a timer, with no knowledge of which messages are still inside handlers, so a rebalance or a
			// crash can leave the group positioned past work that was never completed -- the messages are then
			// never delivered to any member again, with no error and no dead-letter. The transport commits
			// only what it has settled, and that guarantee cannot hold alongside a timer that commits
			// independently of it.
			return ValidateOptionsResult.Fail(
				"KafkaOptions.Consumer.EnableAutoCommit must be false. This transport commits only offsets whose "
				+ "messages have reached a terminal state; a background auto-commit publishes positions on a timer "
				+ "regardless of whether handlers have finished, which can advance the group past in-flight "
				+ "messages and lose them. Leave it false and let the transport commit as messages are settled.");
		}

		return ValidateOptionsResult.Success;
	}
}
