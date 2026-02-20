// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Validates <see cref="CosmosDbCdcStateStoreOptions"/> using the <see cref="IValidateOptions{TOptions}"/> pattern.
/// </summary>
public sealed class CosmosDbCdcStateStoreOptionsValidator : IValidateOptions<CosmosDbCdcStateStoreOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CosmosDbCdcStateStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("CosmosDb CDC state store options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(options.DatabaseId))
		{
			return ValidateOptionsResult.Fail("DatabaseId is required.");
		}

		if (string.IsNullOrWhiteSpace(options.ContainerId))
		{
			return ValidateOptionsResult.Fail("ContainerId is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
