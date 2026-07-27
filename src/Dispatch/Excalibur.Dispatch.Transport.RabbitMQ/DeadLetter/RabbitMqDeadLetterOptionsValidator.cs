// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>Validates <see cref="RabbitMqDeadLetterOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class RabbitMqDeadLetterOptionsValidator : IValidateOptions<RabbitMqDeadLetterOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, RabbitMqDeadLetterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Exchange))
		{
			failures.Add($"{nameof(RabbitMqDeadLetterOptions.Exchange)} must be a non-empty exchange name.");
		}

		if (string.IsNullOrWhiteSpace(options.QueueName))
		{
			failures.Add($"{nameof(RabbitMqDeadLetterOptions.QueueName)} must be a non-empty queue name.");
		}

		if (string.IsNullOrWhiteSpace(options.RoutingKey))
		{
			failures.Add($"{nameof(RabbitMqDeadLetterOptions.RoutingKey)} must be a non-empty routing key.");
		}

		if (options.MaxBatchSize < 1)
		{
			failures.Add($"{nameof(RabbitMqDeadLetterOptions.MaxBatchSize)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
