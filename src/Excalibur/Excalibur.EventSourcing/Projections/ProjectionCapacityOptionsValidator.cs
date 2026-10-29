// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

internal sealed class ProjectionCapacityOptionsValidator : IValidateOptions<ProjectionCapacityOptions>
{
    public ValidateOptionsResult Validate(string? name, ProjectionCapacityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.MaxConcurrentOperations < 1) { failures.Add("ProjectionCapacityOptions.MaxConcurrentOperations must be positive."); }
        if (options.MaxConcurrentReads < 1) { failures.Add("ProjectionCapacityOptions.MaxConcurrentReads must be positive."); }
        if (options.MaxConcurrentWrites < 1) { failures.Add("ProjectionCapacityOptions.MaxConcurrentWrites must be positive."); }
        if (options.MaxConcurrentRebuilds < 1) { failures.Add("ProjectionCapacityOptions.MaxConcurrentRebuilds must be positive."); }
        if (options.QueueLimit < 0) { failures.Add("ProjectionCapacityOptions.QueueLimit must not be negative; zero disables waiting."); }
        if (options.MaxResources < 1) { failures.Add("ProjectionCapacityOptions.MaxResources must be positive."); }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
