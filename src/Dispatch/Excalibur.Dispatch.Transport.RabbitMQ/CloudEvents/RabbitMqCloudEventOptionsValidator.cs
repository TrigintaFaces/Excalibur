// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates <see cref="RabbitMqCloudEventOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
internal sealed class RabbitMqCloudEventOptionsValidator : IValidateOptions<RabbitMqCloudEventOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RabbitMqCloudEventOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.PrefetchCount == 0)
		{
			failures.Add($"{nameof(RabbitMqCloudEventOptions.PrefetchCount)} must be greater than zero.");
		}

		if (options.Exchange.MaxMessageSizeBytes <= 0)
		{
			failures.Add(
				$"{nameof(RabbitMqCloudEventExchangeOptions)}.{nameof(RabbitMqCloudEventExchangeOptions.MaxMessageSizeBytes)} " +
				$"must be greater than zero (was {options.Exchange.MaxMessageSizeBytes}).");
		}

		if (options.DeadLetter.MaxRetryAttempts < 0)
		{
			failures.Add(
				$"{nameof(RabbitMqCloudEventDeadLetterOptions)}.{nameof(RabbitMqCloudEventDeadLetterOptions.MaxRetryAttempts)} " +
				$"must be >= 0 (was {options.DeadLetter.MaxRetryAttempts}).");
		}

		if (options.DeadLetter.RetryDelay <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(RabbitMqCloudEventDeadLetterOptions)}.{nameof(RabbitMqCloudEventDeadLetterOptions.RetryDelay)} " +
				$"must be greater than zero (was {options.DeadLetter.RetryDelay}).");
		}

		if (options.Recovery.NetworkRecoveryInterval <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(RabbitMqCloudEventRecoveryOptions)}.{nameof(RabbitMqCloudEventRecoveryOptions.NetworkRecoveryInterval)} " +
				$"must be greater than zero (was {options.Recovery.NetworkRecoveryInterval}).");
		}

		if (options.Exchange.MessageTtl is { } ttl && ttl <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(RabbitMqCloudEventExchangeOptions)}.{nameof(RabbitMqCloudEventExchangeOptions.MessageTtl)} " +
				$"must be greater than zero when set (was {ttl}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
