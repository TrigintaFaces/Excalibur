// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Validates <see cref="SplunkConnectionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class SplunkConnectionOptionsValidator : IValidateOptions<SplunkConnectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SplunkConnectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.HecEndpoint is null)
		{
			failures.Add($"{nameof(SplunkConnectionOptions.HecEndpoint)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.HecToken))
		{
			failures.Add($"{nameof(SplunkConnectionOptions.HecToken)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
