// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.ErrorHandling;

/// <summary>
/// Validates <see cref="SqlServerDeadLetterOptions"/> at startup.
/// </summary>
internal sealed class SqlServerDeadLetterOptionsValidator : IValidateOptions<SqlServerDeadLetterOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, SqlServerDeadLetterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add($"{nameof(options.ConnectionString)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SchemaName))
        {
            failures.Add($"{nameof(options.SchemaName)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.TableName))
        {
            failures.Add($"{nameof(options.TableName)} is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
