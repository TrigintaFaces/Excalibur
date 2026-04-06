// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Validates <see cref="RabbitMqManagementOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class RabbitMqManagementOptionsValidator : IValidateOptions<RabbitMqManagementOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RabbitMqManagementOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("RabbitMQ management options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.BaseUrl))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ Management BaseUrl is required. Set RabbitMqManagementOptions.BaseUrl to the management API endpoint (e.g., 'http://localhost:15672').");
		}

		if (string.IsNullOrWhiteSpace(options.Username))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ Management Username is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Password))
		{
			return ValidateOptionsResult.Fail(
				"RabbitMQ Management Password is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
