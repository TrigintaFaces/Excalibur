// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.MongoDB.Cdc;

public sealed class MongoDbCdcStateStoreOptionsValidator : IValidateOptions<MongoDbCdcStateStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, MongoDbCdcStateStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("MongoDB CDC state store options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.DatabaseName))
		{
			return ValidateOptionsResult.Fail("DatabaseName is required.");
		}

		if (string.IsNullOrWhiteSpace(options.CollectionName))
		{
			return ValidateOptionsResult.Fail("CollectionName is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
