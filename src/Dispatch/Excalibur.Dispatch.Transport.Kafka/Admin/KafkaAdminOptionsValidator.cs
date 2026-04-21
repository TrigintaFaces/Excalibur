// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Validates <see cref="KafkaAdminOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class KafkaAdminOptionsValidator : IValidateOptions<KafkaAdminOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KafkaAdminOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Kafka admin options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.BootstrapServers))
		{
			return ValidateOptionsResult.Fail(
				"Kafka BootstrapServers is required. Set KafkaAdminOptions.BootstrapServers to the broker addresses (e.g., 'broker1:9092,broker2:9092').");
		}

		return ValidateOptionsResult.Success;
	}
}
