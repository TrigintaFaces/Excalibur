// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.AuditLogging.Alerting;

/// <summary>
/// Validates <see cref="AuditAlertOptions"/> at startup. Reflection-free (AOT-safe).
/// </summary>
internal sealed class AuditAlertOptionsValidator : IValidateOptions<AuditAlertOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AuditAlertOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.MaxAlertsPerMinute < 1
			? ValidateOptionsResult.Fail(
				$"{nameof(AuditAlertOptions.MaxAlertsPerMinute)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
