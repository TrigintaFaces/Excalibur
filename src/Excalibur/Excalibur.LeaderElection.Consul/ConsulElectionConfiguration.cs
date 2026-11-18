// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Consul;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Retry;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Captures configuration and constructs the provider's SDK and retry policy.</summary>
internal static class ConsulElectionConfiguration
{
    internal static ILogger<ConsulLeaderElection> LoggerOrDefault(ILogger<ConsulLeaderElection>? logger) =>
        logger ?? NullLogger<ConsulLeaderElection>.Instance;

    internal static ConsulLeaderElectionOptions Capture(ConsulLeaderElectionOptions source)
    {
        var copy = new ConsulLeaderElectionOptions
        {
            InstanceId = source.InstanceId, LeaseDuration = source.LeaseDuration, RenewInterval = source.RenewInterval,
            RetryInterval = source.RetryInterval, GracePeriod = source.GracePeriod, EnableHealthChecks = source.EnableHealthChecks,
            MinimumHealthScore = source.MinimumHealthScore, StepDownWhenUnhealthy = source.StepDownWhenUnhealthy,
            ConsulAddress = source.ConsulAddress, Datacenter = source.Datacenter, Token = source.Token, KeyPrefix = source.KeyPrefix,
            SessionTTL = source.SessionTTL, LockDelay = source.LockDelay, HealthCheckId = source.HealthCheckId, MaxRetryAttempts = source.MaxRetryAttempts
        };
        foreach (var pair in source.CandidateMetadata) { copy.CandidateMetadata.Add(pair); }
        var validation = new ConsulLeaderElectionOptionsValidator().Validate(null, copy);
        if (validation.Failed) { throw new OptionsValidationException(nameof(ConsulLeaderElectionOptions), typeof(ConsulLeaderElectionOptions), validation.Failures); }
        return copy;
    }

    internal static IConsulClient CreateClient(ConsulLeaderElectionOptions options) => new ConsulClient(config =>
    {
        config.Address = new Uri(options.ConsulAddress);
        if (!string.IsNullOrEmpty(options.Datacenter)) { config.Datacenter = options.Datacenter; }
        if (!string.IsNullOrEmpty(options.Token)) { config.Token = options.Token; }
    });

    // Every operation keeps a fresh options object and the constructor-captured token/datacenter.
    internal static QueryOptions LeaderQueryOptions(ConsulLeaderElectionOptions options) =>
        LeaderQueryOptions(options.Datacenter, options.Token);

    internal static QueryOptions LeaderQueryOptions(string? datacenter, string? token) => new()
    { Consistency = ConsistencyMode.Consistent, Datacenter = datacenter, Token = token };

    internal static QueryOptions HealthQueryOptions(ConsulLeaderElectionOptions options) => new()
    { Datacenter = options.Datacenter, Token = options.Token };

    internal static WriteOptions WriteOptions(ConsulLeaderElectionOptions options) =>
        WriteOptions(options.Datacenter, options.Token);

    internal static WriteOptions WriteOptions(string? datacenter, string? token) => new()
    { Datacenter = datacenter, Token = token };

    internal static ResiliencePipeline CreateRetryPolicy(ConsulLeaderElectionOptions options,
        Action<Exception, int, double> onRetry) => options.MaxRetryAttempts == 0 ? ResiliencePipeline.Empty : new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(TimeSpan.FromSeconds(Math.Min(Math.Pow(2, args.AttemptNumber), 30))),
            ShouldHandle = new PredicateBuilder().Handle<ConsulRequestException>().Handle<HttpRequestException>().Handle<TaskCanceledException>(),
            OnRetry = args => { onRetry(args.Outcome.Exception!, args.AttemptNumber, args.RetryDelay.TotalSeconds); return ValueTask.CompletedTask; }
        }).Build();
}
