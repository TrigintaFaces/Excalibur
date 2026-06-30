// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Threading;

/// <summary>
/// Validates <see cref="ThreadingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// A value of <c>0</c> is permitted for both settings as an "auto" sentinel (the framework
/// derives a default, e.g. from the available CPU core count). Negative values and values
/// beyond the documented valid ranges are rejected so misconfiguration fails fast at startup.
/// </remarks>
internal sealed class ThreadingOptionsValidator : IValidateOptions<ThreadingOptions>
{
	private const int MaxDegreeOfParallelism = 1000;
	private const int MaxPrefetchBufferSize = 10000;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ThreadingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultMaxDegreeOfParallelism is < 0 or > MaxDegreeOfParallelism)
		{
			failures.Add(
				$"{nameof(ThreadingOptions)}.{nameof(ThreadingOptions.DefaultMaxDegreeOfParallelism)} must be between 0 and {MaxDegreeOfParallelism} " +
				$"(was {options.DefaultMaxDegreeOfParallelism}; 0 = auto). " +
				$"Configure it via services.Configure<{nameof(ThreadingOptions)}>(o => o.DefaultMaxDegreeOfParallelism = Environment.ProcessorCount).");
		}

		if (options.PrefetchBufferSize is < 0 or > MaxPrefetchBufferSize)
		{
			failures.Add(
				$"{nameof(ThreadingOptions)}.{nameof(ThreadingOptions.PrefetchBufferSize)} must be between 0 and {MaxPrefetchBufferSize} " +
				$"(was {options.PrefetchBufferSize}; 0 = auto). " +
				$"Configure it via services.Configure<{nameof(ThreadingOptions)}>(o => o.PrefetchBufferSize = 256).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
