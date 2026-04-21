// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Validates <see cref="MongoDbProviderOptions"/> at startup.
/// </summary>
internal sealed class MongoDbProviderOptionsValidator : IValidateOptions<MongoDbProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MongoDbProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{nameof(options.ConnectionString)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            failures.Add($"{nameof(options.DatabaseName)} is required.");
        }

        if (options.ServerSelectionTimeout < 1)
        {
            failures.Add($"{nameof(options.ServerSelectionTimeout)} must be >= 1.");
        }

        if (options.ConnectTimeout < 1)
        {
            failures.Add($"{nameof(options.ConnectTimeout)} must be >= 1.");
        }

        if (options.RetryCount < 0)
        {
            failures.Add($"{nameof(options.RetryCount)} must be >= 0.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
