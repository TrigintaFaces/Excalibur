// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Cdc;

public sealed class PostgresCdcStateStoreOptionsValidator : IValidateOptions<PostgresCdcStateStoreOptions>
{
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

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
