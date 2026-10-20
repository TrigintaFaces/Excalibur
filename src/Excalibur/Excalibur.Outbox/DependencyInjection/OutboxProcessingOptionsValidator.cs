// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.Outbox;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>Validates the final options consumed by the outbox drain loop.</summary>
internal sealed class OutboxProcessingOptionsValidator : IValidateOptions<OutboxProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (!IsSupportedTimeout(options.PollingInterval))
        {
            failures.Add("PollingInterval must be supported by Task.Delay.");
        }
        if (!IsSupportedTimeout(options.DrainTimeout))
        {
            failures.Add("DrainTimeoutSeconds must be supported by CancellationTokenSource.");
        }
        if (options.MaxRetries < 0)
        {
            failures.Add("MaxRetries must be non-negative.");
        }
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsSupportedTimeout(TimeSpan timeout)
    {
        // The runtime truncates fractional milliseconds before validating its timer range.
        var milliseconds = (long)timeout.TotalMilliseconds;
        return milliseconds is >= -1 and <= uint.MaxValue - 1L;
    }
}
