// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AzureServiceBus;

/// <summary>Validates <see cref="ServiceBusDeadLetterOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ServiceBusDeadLetterOptionsValidator : IValidateOptions<ServiceBusDeadLetterOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ServiceBusDeadLetterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxBatchSize < 1)
		{
			failures.Add($"{nameof(ServiceBusDeadLetterOptions.MaxBatchSize)} must be greater than zero.");
		}

		if (options.ReceiveWaitTime <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ServiceBusDeadLetterOptions.ReceiveWaitTime)} must be greater than zero.");
		}

		if (options.StatisticsPeekCount < 1)
		{
			failures.Add($"{nameof(ServiceBusDeadLetterOptions.StatisticsPeekCount)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
