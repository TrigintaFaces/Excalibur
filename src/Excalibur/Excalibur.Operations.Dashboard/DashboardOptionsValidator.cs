// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// Validates <see cref="DashboardOptions"/> at startup, enforcing route-prefix format and page-size
/// bounds so misconfiguration fails fast via <c>ValidateOnStart()</c> rather than at first request.
/// </summary>
internal sealed class DashboardOptionsValidator : IValidateOptions<DashboardOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.RoutePrefix) || !options.RoutePrefix.StartsWith('/'))
		{
			failures.Add($"{nameof(DashboardOptions.RoutePrefix)} must be a non-empty path starting with '/'.");
		}

		if (options.DefaultPageSize < 1)
		{
			failures.Add($"{nameof(DashboardOptions.DefaultPageSize)} must be greater than zero.");
		}

		if (options.MaxPageSize < 1)
		{
			failures.Add($"{nameof(DashboardOptions.MaxPageSize)} must be greater than zero.");
		}

		if (options.DefaultPageSize > options.MaxPageSize)
		{
			failures.Add(
				$"{nameof(DashboardOptions.DefaultPageSize)} ({options.DefaultPageSize}) must not exceed " +
				$"{nameof(DashboardOptions.MaxPageSize)} ({options.MaxPageSize}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
