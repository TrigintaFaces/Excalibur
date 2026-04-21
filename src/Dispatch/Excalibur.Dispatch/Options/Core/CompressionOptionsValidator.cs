// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Validates <see cref="CompressionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class CompressionOptionsValidator : IValidateOptions<CompressionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CompressionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.CompressionLevel is < 0 or > 9)
		{
			failures.Add(
				$"{nameof(CompressionOptions)}.{nameof(CompressionOptions.CompressionLevel)} must be between 0 and 9 (was {options.CompressionLevel}). " +
				$"Configure it via services.Configure<{nameof(CompressionOptions)}>(o => o.CompressionLevel = 6).");
		}

		if (options.MinimumSizeThreshold < 0)
		{
			failures.Add(
				$"{nameof(CompressionOptions)}.{nameof(CompressionOptions.MinimumSizeThreshold)} must be >= 0 (was {options.MinimumSizeThreshold}). " +
				$"Configure it via services.Configure<{nameof(CompressionOptions)}>(o => o.MinimumSizeThreshold = 1024).");
		}

		if (!Enum.IsDefined(options.CompressionType))
		{
			failures.Add(
				$"{nameof(CompressionOptions)}.{nameof(CompressionOptions.CompressionType)} has an invalid value ({(int)options.CompressionType}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
