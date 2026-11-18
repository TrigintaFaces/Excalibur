// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
using k8s;
using k8s.Autorest;
using k8s.Models;
namespace Excalibur.Integration.Tests.LeaderElection;

// Local helper controls are outside the four-class51-row native Kubernetes population.
[Trait("Category", "Integration")]
[Trait("Component", "TestInfrastructure")]
public sealed class KubernetesElectionTestLifetimeShould
{
    [Fact]
    public async Task BindTheActualDerivedXunitLifetimeToAllCreatedElectionCleanup()
    {
        var mapping = typeof(KubernetesLeaderElectionConformanceShould).GetInterfaceMap(typeof(IAsyncDisposable));
        var slot = Array.FindIndex(mapping.InterfaceMethods, method => method.Name == nameof(IAsyncDisposable.DisposeAsync));
        slot.ShouldBeGreaterThanOrEqualTo(0);
        mapping.TargetMethods[slot].DeclaringType.ShouldBe(typeof(KubernetesLeaderElectionConformanceShould),
            "xUnit's inherited IAsyncDisposable contract must dispatch to the deriver, not primary-only base cleanup");
        // Construct the actual deriver without initializing its native container. Interface dispatch
        // exercises its legitimate empty ownership scope; the held/fault controls below bind that helper.
        await using var fixture = new KubernetesContainerFixture();
        var deriver = new KubernetesLeaderElectionConformanceShould(fixture);
        await ((IAsyncLifetime)deriver).DisposeAsync();
    }

    [Fact]
    public async Task RetireEveryElectionBeforeJoiningAHeldTerminalAndDeleteOnlyAfterBothChannelsSettle()
    {
        var owner = new KubernetesElectionTestLifetime();
        var client = A.Fake<IKubernetes>(); var api = A.Fake<ICoordinationV1Operations>();
        A.CallTo(() => client.CoordinationV1).Returns(api);
        var terminal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deletes = 0;
        A.CallTo(api).Where(call => call.Method.Name == "DeleteNamespacedLeaseWithHttpMessagesAsync")
            .WithReturnType<Task<HttpOperationResponse<V1Status>>>().ReturnsLazily(call =>
            {
                firstDisposed.Task.IsCompletedSuccessfully.ShouldBeTrue(); secondDisposed.Task.IsCompletedSuccessfully.ShouldBeTrue();
                terminal.Task.IsCompletedSuccessfully.ShouldBeTrue();
                call.Arguments[0].ShouldBe("exact-lease"); call.Arguments[1].ShouldBe("test-namespace");
                ((CancellationToken)call.Arguments[^1]!).CanBeCanceled.ShouldBeTrue();
                Interlocked.Increment(ref deletes);
                return Task.FromResult(new HttpOperationResponse<V1Status> { Body = new V1Status() });
            });
        owner.OwnClient(client);
        owner.Track(Election(terminal.Task, () => firstDisposed.TrySetResult()), client, "exact-lease", "test-namespace");
        owner.Track(Election(Task.CompletedTask, () => secondDisposed.TrySetResult()), client, "exact-lease", "test-namespace");
        var cleanup = owner.DisposeAsync().AsTask();
        try
        {
            await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5)); await secondDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cleanup.IsCompleted.ShouldBeFalse(); Volatile.Read(ref deletes).ShouldBe(0);
            A.CallTo(() => client.Dispose()).MustNotHaveHappened();
        }
        finally { terminal.TrySetResult(); await cleanup.WaitAsync(TimeSpan.FromSeconds(5)); }
        deletes.ShouldBe(1); A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreserveResourceAndTerminalAndNativeCleanupFaultsWhileStillDisposingAllOwners()
    {
        var owner = new KubernetesElectionTestLifetime(); var client = A.Fake<IKubernetes>(); var api = A.Fake<ICoordinationV1Operations>();
        A.CallTo(() => client.CoordinationV1).Returns(api);
        var resource = new IOException("resource failed"); var terminal = new InvalidOperationException("notification failed");
        var deletion = new IOException("delete failed"); var services = new InvalidOperationException("services failed");
        owner.OwnClient(client);
        var election = Election(Task.FromException(terminal), () => { });
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() => throw resource); // Synchronous throw must not skip the terminal join.
        owner.Track(election, client, "faulted-lease", "default");
        A.CallTo(api).Where(call => call.Method.Name == "DeleteNamespacedLeaseWithHttpMessagesAsync")
            .WithReturnType<Task<HttpOperationResponse<V1Status>>>().Returns(Task.FromException<HttpOperationResponse<V1Status>>(deletion));
        owner.OwnServices(new RecordingDisposal(() => throw services));
        var error = await Record.ExceptionAsync(() => owner.DisposeAsync().AsTask());
        var aggregate = error.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldContain(resource); aggregate.InnerExceptions.ShouldContain(terminal);
        aggregate.InnerExceptions.ShouldContain(deletion); aggregate.InnerExceptions.ShouldContain(services);
        A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CleanTheFirstCandidateWhenTheNextConstructionFailsBeforeItsStart()
    {
        var owner = new KubernetesElectionTestLifetime(); var client = A.Fake<IKubernetes>(); var api = A.Fake<ICoordinationV1Operations>();
        A.CallTo(() => client.CoordinationV1).Returns(api);
        A.CallTo(api).Where(call => call.Method.Name == "DeleteNamespacedLeaseWithHttpMessagesAsync")
            .WithReturnType<Task<HttpOperationResponse<V1Status>>>().Returns(Task.FromResult(new HttpOperationResponse<V1Status> { Body = new V1Status() }));
        var retired = false; var first = Election(Task.CompletedTask, () => retired = true);
        var failure = new InvalidOperationException("second constructor failed");
        var observed = await Record.ExceptionAsync(() => owner.RunAsync(() =>
        {
            owner.OwnClient(client); owner.Track(first, client, "partial-lease", "default");
            throw failure;
        }));
        observed.ShouldBeSameAs(failure); retired.ShouldBeTrue();
        A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly();
    }

    private static ILeaderElection Election(Task terminal, Action retire)
    {
        var election = A.Fake<ILeaderElection>();
        A.CallTo(() => election.NotificationsCompleted).Returns(terminal);
        A.CallTo(() => election.DisposeAsync()).ReturnsLazily(() => { retire(); return ValueTask.CompletedTask; });
        return election;
    }
    private sealed class RecordingDisposal(Action dispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
    }
}
