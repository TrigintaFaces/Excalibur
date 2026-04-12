// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Validates <see cref="InMemoryBusOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class InMemoryBusOptionsValidator : IValidateOptions<InMemoryBusOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InMemoryBusOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxQueueLength < 1)
		{
			failures.Add(
				$"{nameof(InMemoryBusOptions)}.{nameof(InMemoryBusOptions.MaxQueueLength)} must be >= 1 (was {options.MaxQueueLength}). " +
				$"Configure it via services.Configure<{nameof(InMemoryBusOptions)}>(o => o.MaxQueueLength = 1000).");
		}

		if (options.ProcessingDelay < TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(InMemoryBusOptions)}.{nameof(InMemoryBusOptions.ProcessingDelay)} must not be negative (was {options.ProcessingDelay}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
