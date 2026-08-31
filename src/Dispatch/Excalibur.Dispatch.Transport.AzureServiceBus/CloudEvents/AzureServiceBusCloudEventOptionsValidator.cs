// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Validates <see cref="AzureServiceBusCloudEventOptions"/> at startup so a misconfigured Service Bus
/// CloudEvents pipeline fails fast rather than at first send.
/// </summary>
internal sealed class AzureServiceBusCloudEventOptionsValidator : IValidateOptions<AzureServiceBusCloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AzureServiceBusCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AzureServiceBusCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.TimeToLive is { } ttl && ttl <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(options.TimeToLive)} must be positive when set.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
