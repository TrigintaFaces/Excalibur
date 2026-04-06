// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Sentinel;

/// <summary>
/// Validates <see cref="SentinelExporterOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class SentinelExporterOptionsValidator : IValidateOptions<SentinelExporterOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SentinelExporterOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.WorkspaceId))
		{
			failures.Add($"{nameof(SentinelExporterOptions.WorkspaceId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.SharedKey))
		{
			failures.Add($"{nameof(SentinelExporterOptions.SharedKey)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
