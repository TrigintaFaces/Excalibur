// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Validates ordering key options.
/// </summary>
internal sealed class OrderingKeyOptionsValidator : IValidateOptions<OrderingKeyOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OrderingKeyOptions options)
	{
		if (options == null)
		{
			return ValidateOptionsResult.Fail("Options cannot be null.");
		}

		try
		{
			options.Validate();
			return ValidateOptionsResult.Success;
		}
		catch (ArgumentException ex)
		{
			return ValidateOptionsResult.Fail(ex.Message);
		}
	}
}
