// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

internal static class RetryBenchmarkFilter
{
    internal static Func<Exception, bool>? Create(RetryOptions options)
    {
        if (options.RetryableExceptions.Count == 0 && options.NonRetryableExceptions.Count == 0)
        {
            return null;
        }

        return exception => exception is not OperationCanceledException
            && !options.NonRetryableExceptions.Any(type => type.IsInstanceOfType(exception))
            && (options.RetryableExceptions.Count == 0
                || options.RetryableExceptions.Any(type => type.IsInstanceOfType(exception)));
    }
}
