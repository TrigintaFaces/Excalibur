// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Stores.MongoDb;

/// <summary>Validates <see cref="MongoDbComplianceOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class MongoDbComplianceOptionsValidator : IValidateOptions<MongoDbComplianceOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, MongoDbComplianceOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(MongoDbComplianceOptions.ConnectionString)} must be a non-empty connection string.");
		}

		if (string.IsNullOrWhiteSpace(options.DatabaseName))
		{
			failures.Add($"{nameof(MongoDbComplianceOptions.DatabaseName)} must be a non-empty database name.");
		}

		if (options.ServerSelectionTimeoutSeconds < 1)
		{
			failures.Add($"{nameof(MongoDbComplianceOptions.ServerSelectionTimeoutSeconds)} must be greater than zero.");
		}

		if (options.ConnectTimeoutSeconds < 1)
		{
			failures.Add($"{nameof(MongoDbComplianceOptions.ConnectTimeoutSeconds)} must be greater than zero.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
