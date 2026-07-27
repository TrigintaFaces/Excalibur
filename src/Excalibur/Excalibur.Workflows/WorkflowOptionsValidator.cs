// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Workflows;

/// <summary>
/// Validates <see cref="WorkflowOptions"/> at startup.
/// </summary>
internal sealed class WorkflowOptionsValidator : IValidateOptions<WorkflowOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaxReplayEvents <= 0
            ? ValidateOptionsResult.Fail(
                $"{nameof(WorkflowOptions.MaxReplayEvents)} must be greater than zero.")
            : ValidateOptionsResult.Success;
    }
}
