// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.MySql;

/// <summary>
/// Validates <see cref="MySqlProviderOptions"/> at startup.
/// </summary>
internal sealed class MySqlProviderOptionsValidator : IValidateOptions<MySqlProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, MySqlProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{nameof(options.ConnectionString)} is required.");
        }

        if (options.CommandTimeout < 1)
        {
            failures.Add($"{nameof(options.CommandTimeout)} must be >= 1.");
        }

        if (options.ConnectTimeout < 1)
        {
            failures.Add($"{nameof(options.ConnectTimeout)} must be >= 1.");
        }

        if (options.MaxRetryCount < 0)
        {
            failures.Add($"{nameof(options.MaxRetryCount)} must be >= 0.");
        }

        if (options.Pooling.MaxPoolSize < 1)
        {
            failures.Add($"Pooling.{nameof(options.Pooling.MaxPoolSize)} must be >= 1.");
        }

        if (options.Pooling.MinPoolSize < 0)
        {
            failures.Add($"Pooling.{nameof(options.Pooling.MinPoolSize)} must be >= 0.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
