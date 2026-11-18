// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>Validates the selected authority required by the preserving scheduled-delivery runtime.</summary>
internal sealed class SchedulerExecutionSafetyValidator(IServiceProvider services) : IValidateOptions<SchedulerOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SchedulerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.GetService<IScheduleStore>() is IScheduleExecutionFailureStore
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("RetainAndQuarantine requires the selected schedule store to implement IScheduleExecutionFailureStore; upgrade the provider before starting scheduled delivery. Volatile persistence opt-in does not replace execution safety.");
    }
}
