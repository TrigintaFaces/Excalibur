// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance;

/// <summary>Validates <see cref="BreachNotificationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class BreachNotificationOptionsValidator : IValidateOptions<BreachNotificationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, BreachNotificationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.NotificationDeadlineHours < 1
			? ValidateOptionsResult.Fail($"{nameof(BreachNotificationOptions.NotificationDeadlineHours)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
