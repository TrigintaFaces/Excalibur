// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Text.Json;
using Excalibur.LeaderElection.Tests.SqlServer;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.LeaderElection.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class HealthWrapperOwnershipShould
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CanceledHeldDdlStartCannotAcquireLater(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        var reply = Signal(); fixture.Store.Create = _ => reply.Task;
        using var caller = new CancellationTokenSource();
        var starting = fixture.Wrapper.StartAsync(caller.Token);
        await fixture.Store.CreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        caller.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => starting);
        fixture.Starts.ShouldBe(0);
        var stopping = fixture.Wrapper.StopAsync(CancellationToken.None); stopping.IsCompleted.ShouldBeFalse();
        reply.TrySetResult(); await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Starts.ShouldBe(0); fixture.Wrapper.IsLeader.ShouldBeFalse();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task StopJoinsHeldDdlBeforeItCanCompleteAndNeverResurrects(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        var reply = Signal(); fixture.Store.Create = _ => reply.Task;
        var starting = fixture.Wrapper.StartAsync(CancellationToken.None);
        await fixture.Store.CreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = fixture.Wrapper.StopAsync(CancellationToken.None);
        stopping.IsCompleted.ShouldBeFalse(); fixture.Starts.ShouldBe(0);
        reply.TrySetResult(); await Should.ThrowAsync<OperationCanceledException>(() => starting);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5)); fixture.Starts.ShouldBe(0);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task DisposeRetiresPromptlyAndJoinsActualHeldUpdateAndRead(bool postgres)
    {
        var fixture = new Harness(postgres);
        await fixture.Wrapper.StartAsync(CancellationToken.None);
        var update = Signal(); var read = Signal();
        fixture.Store.Update = _ => update.Task;
        fixture.Store.Read = async _ => { await read.Task; return Array.Empty<CandidateHealth>(); };
        var updating = fixture.Wrapper.UpdateHealthAsync(true, null, CancellationToken.None);
        var reading = fixture.Wrapper.GetCandidateHealthAsync(CancellationToken.None);
        await fixture.Store.UpdateEntered.Task; await fixture.Store.ReadEntered.Task;
        var disposing = fixture.Wrapper.DisposeAsync().AsTask();
        fixture.Wrapper.IsLeader.ShouldBeFalse(); disposing.IsCompleted.ShouldBeFalse();
        fixture.Store.UpdateToken.IsCancellationRequested.ShouldBeTrue(); fixture.Store.ReadToken.IsCancellationRequested.ShouldBeTrue();
        update.TrySetResult(); await updating; disposing.IsCompleted.ShouldBeFalse();
        read.TrySetResult(); await reading; await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.DisposeAsync();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CopyMetadataBeforeAnAsynchronouslyHeldNativeWrite(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        var reply = Signal(); fixture.Store.Update = _ => reply.Task;
        var metadata = new Dictionary<string, string> { ["version"] = "original" };
        var updating = fixture.Wrapper.UpdateHealthAsync(true, metadata, CancellationToken.None);
        await fixture.Store.UpdateEntered.Task;
        metadata["version"] = "mutated"; metadata["extra"] = "later";
        reply.TrySetResult(); await updating;
        using var parsed = JsonDocument.Parse(fixture.Store.Metadata!);
        parsed.RootElement.GetProperty("version").GetString().ShouldBe("original");
        parsed.RootElement.TryGetProperty("extra", out _).ShouldBeFalse();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task PermitNormalLeadershipCallbackStopAndDisposeReentry(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        await fixture.Wrapper.StartAsync(CancellationToken.None);
#pragma warning disable RS0030 // Deliberate synchronous consumer callback reentry adversary.
        fixture.Wrapper.BecameLeader += (_, _) => fixture.Wrapper.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        fixture.Inner.BecameLeader += Raise.With(new LeaderElectionEventArgs("candidate", "resource"));
        fixture.Wrapper.LostLeadership += (_, _) => fixture.Wrapper.DisposeAsync().AsTask().GetAwaiter().GetResult();
        fixture.Inner.LostLeadership += Raise.With(new LeaderElectionEventArgs("candidate", "resource"));
#pragma warning restore RS0030
        fixture.Wrapper.IsLeader.ShouldBeFalse(); fixture.Disposals.ShouldBe(1);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task RejectLifecycleReentryInsideTheActualOwnedHealthSdkScope(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        fixture.Store.Update = async _ =>
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Wrapper.StopAsync(CancellationToken.None));
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.Wrapper.DisposeAsync().AsTask());
        };
        await fixture.Wrapper.UpdateHealthAsync(true, null, CancellationToken.None);
        await fixture.Wrapper.StartAsync(CancellationToken.None); fixture.Wrapper.IsLeader.ShouldBeTrue();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task RetryFailedDdlOnlyAfterOwnedRollbackHasQuiesced(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        fixture.Store.Create = _ => fixture.Store.Creates == 1 ? Task.FromException(new IOException("DDL unavailable")) : Task.CompletedTask;
        await Should.ThrowAsync<IOException>(() => fixture.Wrapper.StartAsync(CancellationToken.None));
        await fixture.Wrapper.StartAsync(CancellationToken.None);
        fixture.Store.Creates.ShouldBe(2); fixture.Starts.ShouldBe(1); fixture.Wrapper.IsLeader.ShouldBeTrue();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task NeverDeleteUnownedMetadataOnFreshStopAndDispose(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        await fixture.Wrapper.StopAsync(CancellationToken.None);
        await fixture.Wrapper.DisposeAsync();
        fixture.Store.Creates.ShouldBe(0); fixture.Store.Removes.ShouldBe(0);
        fixture.Store.UpdateEntered.Task.IsCompleted.ShouldBeFalse();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task RemoveAfterAnAmbiguousWriteAttemptAndResetOnlyOnConfirmedDeletion(bool postgres)
    {
        await using var fixture = new Harness(postgres);
        fixture.Store.Update = _ => Task.FromException(new IOException("write reply lost"));
        await Should.ThrowAsync<IOException>(() => fixture.Wrapper.UpdateHealthAsync(true, null, CancellationToken.None));
        await fixture.Wrapper.StopAsync(CancellationToken.None);
        fixture.Store.Removes.ShouldBe(1);
        await fixture.Wrapper.StopAsync(CancellationToken.None);
        fixture.Store.Removes.ShouldBe(1);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task RejectStartFromAnActualActorNotificationBeforeWrapperCoalescing(bool postgres)
    {
        // Both wrappers receive the real shared-kernel notification context from a real SQL actor.
        // The store is controlled; this is not FakeItEasy Raise and cannot bypass dispatch scope.
        await using var actor = new SqlServerTenureHarness();
        var store = new Store();
        IHealthBasedLeaderElection wrapper = postgres
            ? new PostgresHealthBasedLeaderElection(Options.Create(new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost" }),
                Options.Create(new PostgresHealthBasedLeaderElectionOptions()), NullLogger<PostgresHealthBasedLeaderElection>.Instance, actor.Sut, store)
            : new SqlServerHealthBasedLeaderElection(Options.Create(new SqlServerHealthBasedLeaderElectionOptions { ConnectionString = "Server=localhost", LockResource = "resource" }),
                NullLogger<SqlServerHealthBasedLeaderElection>.Instance, actor.Sut, store);
        var callback = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        wrapper.BecameLeader += (_, _) =>
        {
            try
            {
#pragma warning disable RS0030 // Real actor notification callback attempts synchronous reentrant Start, the actual regression trigger.
                wrapper.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore RS0030
                callback.TrySetResult(null);
            }
            catch (Exception error) { callback.TrySetResult(error); }
        };
        try
        {
            await wrapper.StartAsync(CancellationToken.None);
            (await callback.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeOfType<InvalidOperationException>();
            store.Creates.ShouldBe(1);
        }
        finally { await wrapper.DisposeAsync(); }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Harness : IAsyncDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Harness disposes Wrapper, which owns and joins Inner disposal; calling Inner separately would bypass the ownership contract under test.")]
        internal readonly ILeaderElection Inner = A.Fake<ILeaderElection>();
        internal readonly Store Store = new();
        internal IHealthBasedLeaderElection Wrapper { get; }
        internal int Starts; internal int Disposals; private bool _leader;
        internal Harness(bool postgres)
        {
            A.CallTo(() => Inner.CandidateId).Returns("candidate");
            A.CallTo(() => Inner.IsLeader).ReturnsLazily(() => _leader);
            A.CallTo(() => Inner.StartAsync(A<CancellationToken>._)).ReturnsLazily(() => { Starts++; _leader = true; return Task.CompletedTask; });
            A.CallTo(() => Inner.StopAsync(A<CancellationToken>._)).ReturnsLazily(() => { _leader = false; return Task.CompletedTask; });
            A.CallTo(() => Inner.DisposeAsync()).ReturnsLazily(() => { Disposals++; _leader = false; return ValueTask.CompletedTask; });
            Wrapper = postgres
                ? new PostgresHealthBasedLeaderElection(Options.Create(new PostgresLeaderElectionOptions { ConnectionString = "Host=localhost" }),
                    Options.Create(new PostgresHealthBasedLeaderElectionOptions()), NullLogger<PostgresHealthBasedLeaderElection>.Instance, Inner, Store)
                : new SqlServerHealthBasedLeaderElection(Options.Create(new SqlServerHealthBasedLeaderElectionOptions { ConnectionString = "Server=localhost", LockResource = "resource" }),
                    NullLogger<SqlServerHealthBasedLeaderElection>.Instance, Inner, Store);
        }
        public ValueTask DisposeAsync() => Wrapper.DisposeAsync();
    }

    private sealed class Store : IHealthElectionStore
    {
        internal Func<CancellationToken, Task> Create = _ => Task.CompletedTask;
        internal Func<CancellationToken, Task> Update = _ => Task.CompletedTask;
        internal Func<CancellationToken, Task<IEnumerable<CandidateHealth>>> Read = _ => Task.FromResult<IEnumerable<CandidateHealth>>(Array.Empty<CandidateHealth>());
        internal readonly TaskCompletionSource CreateEntered = Signal();
        internal readonly TaskCompletionSource UpdateEntered = Signal();
        internal readonly TaskCompletionSource ReadEntered = Signal();
        internal CancellationToken UpdateToken; internal CancellationToken ReadToken;
        internal string? Metadata; internal int Creates; internal int Removes;
        public Task CreateAsync(CancellationToken token) { Creates++; CreateEntered.TrySetResult(); return Create(token); }
        public Task UpsertAsync(string candidateId, bool healthy, string metadataJson, bool leader, CancellationToken token)
        { Metadata = metadataJson; UpdateToken = token; UpdateEntered.TrySetResult(); return Update(token); }
        public Task<IEnumerable<CandidateHealth>> ReadAsync(CancellationToken token)
        { ReadToken = token; ReadEntered.TrySetResult(); return Read(token); }
        public Task RemoveAsync(string candidateId, CancellationToken token) { Removes++; return Task.CompletedTask; }
    }
}
