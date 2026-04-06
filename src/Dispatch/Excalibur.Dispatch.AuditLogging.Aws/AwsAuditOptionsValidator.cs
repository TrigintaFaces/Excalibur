// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Aws;

/// <summary>
/// Validates <see cref="AwsAuditOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class AwsAuditOptionsValidator : IValidateOptions<AwsAuditOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsAuditOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.LogGroupName))
		{
			failures.Add($"{nameof(AwsAuditOptions.LogGroupName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Region))
		{
			failures.Add($"{nameof(AwsAuditOptions.Region)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
