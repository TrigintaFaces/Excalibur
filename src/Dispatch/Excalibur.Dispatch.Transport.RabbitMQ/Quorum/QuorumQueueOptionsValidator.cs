// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Validates <see cref="QuorumQueueOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class QuorumQueueOptionsValidator : IValidateOptions<QuorumQueueOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, QuorumQueueOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Quorum queue options cannot be null.");
		}

		if (options.DeliveryLimit.HasValue && options.DeliveryLimit.Value < 1)
		{
			return ValidateOptionsResult.Fail(
				$"DeliveryLimit must be >= 1 when specified, but was {options.DeliveryLimit.Value}.");
		}

		if (options.QuorumSize.HasValue && options.QuorumSize.Value is < 1 or > 99)
		{
			return ValidateOptionsResult.Fail(
				$"QuorumSize must be between 1 and 99 when specified, but was {options.QuorumSize.Value}.");
		}

		return ValidateOptionsResult.Success;
	}
}
