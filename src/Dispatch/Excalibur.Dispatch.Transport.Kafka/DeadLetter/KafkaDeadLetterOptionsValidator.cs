// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates <see cref="KafkaDeadLetterOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class KafkaDeadLetterOptionsValidator : IValidateOptions<KafkaDeadLetterOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KafkaDeadLetterOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Kafka dead letter options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.TopicSuffix))
		{
			return ValidateOptionsResult.Fail(
				"KafkaDeadLetterOptions.TopicSuffix is required. Set it to a non-empty suffix (for example, \".dead-letter\").");
		}

		if (options.MaxDeliveryAttempts < 1)
		{
			return ValidateOptionsResult.Fail(
				"KafkaDeadLetterOptions.MaxDeliveryAttempts must be at least 1.");
		}

		return ValidateOptionsResult.Success;
	}
}
