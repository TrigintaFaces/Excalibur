// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Workflows;

/// <summary>
/// Validates the registered workflow definition set at startup: no duplicate (name, version) registration
/// and every version at least 1. Runs under <c>ValidateOnStart</c> so a mis-registration fails fast at
/// composition rather than on the first workflow start.
/// </summary>
/// <remarks>
/// Construction of <see cref="WorkflowRegistry"/> is the single source of truth for well-formedness — this
/// validator builds it and surfaces any construction failure as an options validation error, so the rule set
/// never drifts between the two.
/// </remarks>
internal sealed class WorkflowRegistryValidator : IValidateOptions<WorkflowOptions>
{
    private readonly IEnumerable<WorkflowDescriptor> _descriptors;

    public WorkflowRegistryValidator(IEnumerable<WorkflowDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = descriptors;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        try
        {
            _ = new WorkflowRegistry(_descriptors);
            return ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
