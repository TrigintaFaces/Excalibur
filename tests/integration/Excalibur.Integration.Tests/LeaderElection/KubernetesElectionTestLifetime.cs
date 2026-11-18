// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using System.Runtime.ExceptionServices;
using Excalibur.Dispatch.LeaderElection;
using k8s;
using k8s.Autorest;
namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>Owns one test's elections, exact native records, service providers and borrowed clients.</summary>
// The reviewed fixtures use one serialized controller; this is not a concurrent ownership registry.
internal sealed class KubernetesElectionTestLifetime : IAsyncDisposable
{
    private readonly List<ILeaderElection> _elections = [];
    private readonly List<(IKubernetes Client, string Name, string Namespace)> _leases = [];
    private readonly List<IAsyncDisposable> _services = [];
    private readonly List<IKubernetes> _clients = [];
    private Task? _cleanup;

    internal IKubernetes OwnClient(IKubernetes client)
    {
        _clients.Add(client);
        return client;
    }
    internal void OwnServices(IAsyncDisposable services) => _services.Add(services);
    internal T Track<T>(T election, IKubernetes client, string leaseName, string leaseNamespace) where T : ILeaderElection
    {
        _elections.Add(election); // Register each successful constructor before constructing the next candidate.
        RecordLease(client, leaseName, leaseNamespace);
        return election;
    }
    internal void RecordLease(IKubernetes client, string name, string leaseNamespace)
    {
        if (!_leases.Any(entry => ReferenceEquals(entry.Client, client) && entry.Name == name && entry.Namespace == leaseNamespace))
        {
            _leases.Add((client, name, leaseNamespace));
        }
    }

    internal async Task RunAsync(Func<Task> body)
    {
        var failures = new List<Exception>();
        try { await body().ConfigureAwait(false); }
        catch (Exception failure) { Add(failures, failure); }
        try { await DisposeAsync().ConfigureAwait(false); }
        catch (Exception failure) { Add(failures, failure); }
        Throw(failures);
    }

    public ValueTask DisposeAsync() => new(_cleanup ??= CleanupAsync());

    private async Task CleanupAsync()
    {
        var failures = new List<Exception>();
        var elections = _elections.ToArray();
        // Normalize synchronous throws and initiate EVERY permanent retirement before awaiting any tail.
        var resources = elections.Select(DisposeResourcesAsync).ToArray();
        var notifications = elections.Select(JoinNotificationsAsync).ToArray();
        await ObserveAllAsync(resources.Concat(notifications).ToArray(), failures).ConfigureAwait(false);

        // The actual SDK call has an independent finite token. Never timeout a wrapper wait and dispose
        // its borrowed client while an ignored-cancellation SDK operation could still be running.
        foreach (var (client, name, leaseNamespace) in _leases)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                _ = await client.CoordinationV1.DeleteNamespacedLeaseAsync(name, leaseNamespace,
                    cancellationToken: cleanup.Token).ConfigureAwait(false);
            }
            catch (HttpOperationException failure) when (failure.Response?.StatusCode == HttpStatusCode.NotFound) { }
            catch (Exception failure) { Add(failures, failure); }
        }
        foreach (var services in _services)
        {
            try { await services.DisposeAsync().ConfigureAwait(false); }
            catch (Exception failure) { Add(failures, failure); }
        }
        foreach (var client in _clients)
        {
            // AddSingleton(instance) does not transfer ownership of this external native client to DI.
            try { client.Dispose(); }
            catch (Exception failure) { Add(failures, failure); }
        }
        Throw(failures);
    }

    private static async Task DisposeResourcesAsync(ILeaderElection election) => await election.DisposeAsync().ConfigureAwait(false);
    private static async Task JoinNotificationsAsync(ILeaderElection election) => await election.NotificationsCompleted.ConfigureAwait(false);
    private static async Task ObserveAllAsync(Task[] tasks, List<Exception> failures)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (Exception)
        {
            foreach (var task in tasks)
            {
                if (task.Exception is { } failure) { Add(failures, failure); }
                else if (task.IsCanceled)
                {
                    try { await task.ConfigureAwait(false); }
                    catch (Exception cancellationFailure) { Add(failures, cancellationFailure); }
                }
            }
        }
    }
    private static void Add(List<Exception> failures, Exception failure)
    {
        if (failure is AggregateException { InnerExceptions.Count: > 0 } aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions) { Add(failures, inner); }
        }
        else if (!failures.Any(previous => ReferenceEquals(previous, failure))) { failures.Add(failure); }
    }
    private static void Throw(List<Exception> failures)
    {
        if (failures.Count == 1) { ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException("Kubernetes fixture body and cleanup failed.", failures); }
    }
}
