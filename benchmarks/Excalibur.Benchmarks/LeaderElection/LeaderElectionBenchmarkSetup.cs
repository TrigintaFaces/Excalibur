// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Benchmarks.LeaderElection;

/// <summary>Owns candidate construction and drained cleanup outside benchmark timing.</summary>
internal static class LeaderElectionBenchmarkSetup
{
    internal static InMemoryLeaderElection[] CreateCandidates(int count, LeaderElectionOptions template)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(template);
        var candidates = new InMemoryLeaderElection[count];
        var resourceName = $"benchmark-resource-{Guid.NewGuid():N}";
        var sharedState = new InMemoryLeaderElectionSharedState();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var options = Options.Create(new LeaderElectionOptions
                {
                    LeaseDuration = template.LeaseDuration,
                    RenewInterval = template.RenewInterval,
                    RetryInterval = template.RetryInterval,
                    StepDownWhenUnhealthy = template.StepDownWhenUnhealthy,
                    InstanceId = $"candidate-{i}",
                });
                candidates[i] = new InMemoryLeaderElection(
                    resourceName, options,
                    NullLogger<InMemoryLeaderElection>.Instance, sharedState);
            }

            return candidates;
        }
        catch (Exception constructionFailure)
        {
            try
            {
                DisposeCandidatesAsync(candidates).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(constructionFailure, cleanupFailure);
            }

            throw;
        }
    }

    internal static async ValueTask DisposeCandidatesAsync(InMemoryLeaderElection[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        Exception? failure = null;
        foreach (var candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            try
            {
                await candidate.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                failure = failure is null ? error : new AggregateException(failure, error);
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
