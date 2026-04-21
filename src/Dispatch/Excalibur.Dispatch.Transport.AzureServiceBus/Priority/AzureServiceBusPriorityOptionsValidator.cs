// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Validates <see cref="AzureServiceBusPriorityOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AzureServiceBusPriorityOptionsValidator : IValidateOptions<AzureServiceBusPriorityOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AzureServiceBusPriorityOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Azure Service Bus priority options cannot be null.");
		}

		if (options.PriorityLevels is < 2 or > 10)
		{
			return ValidateOptionsResult.Fail(
				$"PriorityLevels must be between 2 and 10, but was {options.PriorityLevels}.");
		}

		if (string.IsNullOrWhiteSpace(options.QueueNameTemplate))
		{
			return ValidateOptionsResult.Fail(
				"QueueNameTemplate is required. Set AzureServiceBusPriorityOptions.QueueNameTemplate to a template with a {{0}} placeholder (e.g., 'dispatch-priority-{0}').");
		}

		if (options.DefaultPriority is < 0 or > 9)
		{
			return ValidateOptionsResult.Fail(
				$"DefaultPriority must be between 0 and 9, but was {options.DefaultPriority}.");
		}

		return ValidateOptionsResult.Success;
	}
}
