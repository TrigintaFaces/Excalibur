// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>
/// Validates <see cref="ArchivePolicy"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class ArchivePolicyValidator : IValidateOptions<ArchivePolicy>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ArchivePolicy options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxAge is null && options.MaxPosition is null && options.RetainRecentCount is null)
		{
			failures.Add("At least one archive criterion must be set (MaxAge, MaxPosition, or RetainRecentCount).");
		}

		if (options.MaxAge is { } maxAge && maxAge <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ArchivePolicy.MaxAge)} must be greater than TimeSpan.Zero when set (was {maxAge}).");
		}

		if (options.RetainRecentCount is { } retainCount && retainCount <= 0)
		{
			failures.Add($"{nameof(ArchivePolicy.RetainRecentCount)} must be greater than 0 when set (was {retainCount}).");
		}

		if (options.MaxPosition is { } maxPos && maxPos <= 0)
		{
			failures.Add($"{nameof(ArchivePolicy.MaxPosition)} must be greater than 0 when set (was {maxPos}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
