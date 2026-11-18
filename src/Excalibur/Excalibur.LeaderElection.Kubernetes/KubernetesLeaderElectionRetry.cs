// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Autorest;
using Polly;
using Polly.Retry;

namespace Excalibur.LeaderElection.Kubernetes;

internal static class KubernetesLeaderElectionRetry
{
    internal static ResiliencePipeline Create(KubernetesLeaderElectionOptions options, TimeProvider clock,
        Action<Exception, int, double> onRetry)
    {
        var builder = new ResiliencePipelineBuilder { TimeProvider = clock };
        if (options.MaxRetries == 0) { return builder.Build(); }
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetries, Delay = TimeSpan.Zero,
            ShouldHandle = new PredicateBuilder().Handle<HttpOperationException>(static error => IsTransient(error.Response?.StatusCode)).Handle<TaskCanceledException>(),
            OnRetry = async args =>
            {
                var exponential = args.AttemptNumber >= 40 ? TimeSpan.MaxValue : TimeSpan.FromTicks(TimeSpan.TicksPerSecond * (1L << args.AttemptNumber));
                var delay = exponential < options.MaxRetryDelay ? exponential : options.MaxRetryDelay;
                onRetry(args.Outcome.Exception!, args.AttemptNumber, delay.TotalMilliseconds);
                if (delay > TimeSpan.Zero) { await LeaderElectionDelay.WaitAsync(delay, clock, args.Context.CancellationToken).ConfigureAwait(false); }
            }
        });
        return builder.Build();
    }
    private static bool IsTransient(HttpStatusCode? status) => status is null || (int)status.Value >= 500 ||
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
}
