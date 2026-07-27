// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Oracle.DependencyInjection;

/// <summary>
/// Validates <see cref="OracleEventStoreOptions"/> at startup via <c>ValidateOnStart</c>.
/// </summary>
internal sealed class OracleEventStoreOptionsValidator : IValidateOptions<OracleEventStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, OracleEventStoreOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail("Oracle event store requires a non-empty ConnectionString.");
		}

		if (string.IsNullOrWhiteSpace(options.Schema))
		{
			return ValidateOptionsResult.Fail("Oracle event store requires a non-empty Schema.");
		}

		if (string.IsNullOrWhiteSpace(options.Table))
		{
			return ValidateOptionsResult.Fail("Oracle event store requires a non-empty Table.");
		}

		return ValidateOptionsResult.Success;
	}
}
