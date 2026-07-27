// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Governance.Provisioning;

/// <summary>Validates <see cref="ProvisioningOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ProvisioningOptionsValidator : IValidateOptions<ProvisioningOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ProvisioningOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.DefaultApprovalTimeout <= TimeSpan.Zero
			? ValidateOptionsResult.Fail($"{nameof(ProvisioningOptions.DefaultApprovalTimeout)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
