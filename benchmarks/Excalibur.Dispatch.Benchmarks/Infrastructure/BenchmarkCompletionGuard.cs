// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Benchmarks.Infrastructure;

/// <summary>
/// Verifies completion observed after an invocation, outside measured benchmark methods.
/// </summary>
internal static class BenchmarkCompletionGuard
{
    internal static async Task VerifyCompletedAsync(Task invocation, Task completion)
    {
        await invocation.ConfigureAwait(false);
        if (!completion.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException("Benchmark handler completion was not observed when invocation returned.");
        }
    }
}
