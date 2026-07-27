// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates <see cref="KafkaCloudEventOptions"/> at startup so a misconfigured Kafka CloudEvents pipeline
/// fails fast (via <c>ValidateOnStart()</c>) rather than at first produce.
/// </summary>
internal sealed class KafkaCloudEventOptionsValidator : IValidateOptions<KafkaCloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, KafkaCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(KafkaCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.DefaultPartitionCount <= 0)
		{
			failures.Add($"{nameof(options.DefaultPartitionCount)} must be greater than zero.");
		}

		if (options.DefaultReplicationFactor <= 0)
		{
			failures.Add($"{nameof(options.DefaultReplicationFactor)} must be greater than zero.");
		}

		if (options.Producer is null)
		{
			failures.Add($"{nameof(options.Producer)} must not be null.");
		}
		else if (options.Producer.MaxMessageSizeBytes <= 0)
		{
			failures.Add($"{nameof(options.Producer)}.{nameof(options.Producer.MaxMessageSizeBytes)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
