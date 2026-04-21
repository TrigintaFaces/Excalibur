// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Validates <see cref="RabbitMqStreamOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class RabbitMqStreamOptionsValidator : IValidateOptions<RabbitMqStreamOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RabbitMqStreamOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("RabbitMQ stream options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.StreamName))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ StreamName is required. Set RabbitMqStreamOptions.StreamName to the target stream queue name.");
		}

		if (options.SegmentSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"SegmentSize must be >= 1, but was {options.SegmentSize}.");
		}

		return ValidateOptionsResult.Success;
	}
}
