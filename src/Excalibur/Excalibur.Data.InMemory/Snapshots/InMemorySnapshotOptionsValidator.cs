// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Snapshots;

/// <summary>
/// Validates <see cref="InMemorySnapshotOptions"/> at startup.
/// </summary>
internal sealed class InMemorySnapshotOptionsValidator : IValidateOptions<InMemorySnapshotOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, InMemorySnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MaxSnapshots < 0)
        {
            failures.Add($"{nameof(options.MaxSnapshots)} must be >= 0.");
        }

        if (options.MaxSnapshotsPerAggregate < 0)
        {
            failures.Add($"{nameof(options.MaxSnapshotsPerAggregate)} must be >= 0.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
