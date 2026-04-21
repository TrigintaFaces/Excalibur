// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Validates <see cref="DispatchOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class DispatchOptionsValidator : IValidateOptions<DispatchOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DispatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultTimeout <= TimeSpan.Zero)
		{
			failures.Add(
				$"{nameof(DispatchOptions)}.{nameof(DispatchOptions.DefaultTimeout)} must be positive (was {options.DefaultTimeout}). " +
				$"Configure it via services.Configure<{nameof(DispatchOptions)}>(o => o.DefaultTimeout = TimeSpan.FromSeconds(30)).");
		}

		if (options.MaxConcurrency < 1)
		{
			failures.Add(
				$"{nameof(DispatchOptions)}.{nameof(DispatchOptions.MaxConcurrency)} must be >= 1 (was {options.MaxConcurrency}). " +
				$"Configure it via services.Configure<{nameof(DispatchOptions)}>(o => o.MaxConcurrency = Environment.ProcessorCount).");
		}

		if (options.MessageBufferSize < 1)
		{
			failures.Add(
				$"{nameof(DispatchOptions)}.{nameof(DispatchOptions.MessageBufferSize)} must be >= 1 (was {options.MessageBufferSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
