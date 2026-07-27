// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Validates <see cref="PulsarOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class PulsarOptionsValidator : IValidateOptions<PulsarOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PulsarOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Pulsar options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.ServiceUrl))
		{
			return ValidateOptionsResult.Fail(
				"Pulsar ServiceUrl is required. Set PulsarOptions.ServiceUrl to a broker URL such as pulsar://localhost:6650.");
		}

		if (string.IsNullOrWhiteSpace(options.SubscriptionName))
		{
			return ValidateOptionsResult.Fail(
				"Pulsar SubscriptionName is required. It is the durable consumer identity shared by competing consumers.");
		}

		if (options.Receive is { MaxBatchSize: < 1 })
		{
			return ValidateOptionsResult.Fail(
				"PulsarOptions.Receive.MaxBatchSize must be at least 1.");
		}

		if (options.Receive is { MaxPayloadBytes: < 1 })
		{
			return ValidateOptionsResult.Fail(
				"PulsarOptions.Receive.MaxPayloadBytes must be at least 1 byte when specified. Set it to null to opt out of the payload-size limit.");
		}

		return ValidateOptionsResult.Success;
	}
}
