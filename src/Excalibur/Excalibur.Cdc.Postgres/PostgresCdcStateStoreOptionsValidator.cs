// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Validates <see cref="PostgresCdcStateStoreOptions"/> ensuring required fields are present and contain valid SQL identifiers.
/// </summary>
public sealed class PostgresCdcStateStoreOptionsValidator : IValidateOptions<PostgresCdcStateStoreOptions>
{
	/// <summary>
	/// Validates the specified <see cref="PostgresCdcStateStoreOptions"/> instance.
	/// </summary>
	/// <param name="name">The name of the options instance being validated.</param>
	/// <param name="options">The options instance to validate.</param>
	/// <returns>A <see cref="ValidateOptionsResult"/> indicating success or describing validation failures.</returns>
	public ValidateOptionsResult Validate(string? name, PostgresCdcStateStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Postgres CDC state store options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		return ValidateOptionsResult.Success;
	}
}
