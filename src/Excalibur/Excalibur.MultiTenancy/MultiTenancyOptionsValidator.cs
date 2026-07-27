// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.MultiTenancy;

/// <summary>
/// Validates <see cref="MultiTenancyOptions"/> at startup: the isolation strategy must be an explicitly chosen,
/// defined value (never <see cref="TenantIsolationStrategy.Unspecified"/> or an out-of-range value).
/// </summary>
/// <remarks>
/// This validator only checks the value shape of <see cref="MultiTenancyOptions"/>. Whether a decoratable store
/// is actually registered for the row-discriminator strategy is a service-collection concern that
/// <see cref="IValidateOptions{TOptions}"/> cannot observe, so it is enforced fail-fast inside
/// <c>AddMultiTenancy</c> at composition time instead.
/// </remarks>
internal sealed class MultiTenancyOptionsValidator : IValidateOptions<MultiTenancyOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, MultiTenancyOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Strategy == TenantIsolationStrategy.Unspecified || !Enum.IsDefined(options.Strategy))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(MultiTenancyOptions)}.{nameof(MultiTenancyOptions.Strategy)} must be set to a valid tenant "
				+ $"isolation strategy ({nameof(TenantIsolationStrategy.RowDiscriminator)} or "
				+ $"{nameof(TenantIsolationStrategy.Sharding)}).");
		}

		return ValidateOptionsResult.Success;
	}
}
