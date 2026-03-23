// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>
/// Validates <see cref="AzureServiceBusOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AzureServiceBusOptionsValidator : IValidateOptions<AzureServiceBusOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AzureServiceBusOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Azure Service Bus options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.Namespace) && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"Azure Service Bus requires either Namespace or ConnectionString to be configured. " +
				"Set AzureServiceBusOptions.Namespace for managed identity or AzureServiceBusOptions.ConnectionString for connection string auth.");
		}

		if (string.IsNullOrWhiteSpace(options.QueueName))
		{
			return ValidateOptionsResult.Fail(
				"Azure Service Bus QueueName is required. Set AzureServiceBusOptions.QueueName to the target queue or topic.");
		}

		return ValidateOptionsResult.Success;
	}
}
