// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Azure;

/// <summary>Validates <see cref="AzureLogicAppsOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class AzureLogicAppsOptionsValidator : IValidateOptions<AzureLogicAppsOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AzureLogicAppsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ResourceGroupName))
		{
			failures.Add($"{nameof(AzureLogicAppsOptions.ResourceGroupName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.SubscriptionId))
		{
			failures.Add($"{nameof(AzureLogicAppsOptions.SubscriptionId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.JobExecutionEndpoint))
		{
			failures.Add($"{nameof(AzureLogicAppsOptions.JobExecutionEndpoint)} is required.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
