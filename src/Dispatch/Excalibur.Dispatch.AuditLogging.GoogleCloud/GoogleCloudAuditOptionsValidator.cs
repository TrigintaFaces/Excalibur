// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.GoogleCloud;

/// <summary>
/// Validates <see cref="GoogleCloudAuditOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class GoogleCloudAuditOptionsValidator : IValidateOptions<GoogleCloudAuditOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GoogleCloudAuditOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ProjectId))
		{
			return ValidateOptionsResult.Fail($"{nameof(GoogleCloudAuditOptions.ProjectId)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
