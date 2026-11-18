// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s.Models;
namespace Excalibur.LeaderElection.Tests.Kubernetes;
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
[Trait("Pattern", "Regression")]
public sealed class KubernetesRetriesOnlyTransientFailuresShould
{
    public static TheoryData<HttpStatusCode> DefinitiveStatuses => Partition(false);
    public static TheoryData<HttpStatusCode> TransientStatuses => Partition(true);
    private static TheoryData<HttpStatusCode> Partition(bool transient)
    {
        var result = new TheoryData<HttpStatusCode>();
        foreach (var status in Enum.GetValues<HttpStatusCode>().Concat([(HttpStatusCode)499, (HttpStatusCode)599]).DistinctBy(status => (int)status).OrderBy(status => (int)status))
        {
            if (((int)status >= 500 || status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests) == transient) { result.Add(status); }
        }
        return result;
    }
    [Theory]
    [MemberData(nameof(DefinitiveStatuses))]
    public Task AttemptADefinitiveStatusExactlyOnceWhicheverOneItIs(HttpStatusCode status) => AssertAttempts(status, 1);
    [Theory]
    [MemberData(nameof(TransientStatuses))]
    public Task RetryATransientStatusWhicheverOneItIs(HttpStatusCode status) => AssertAttempts(status, 4);
    [Fact]
    public Task AttemptAConflictedWriteExactlyOnceBeforeHandlingTheConflict() => AssertAttempts(HttpStatusCode.Conflict, 1);
    private static async Task AssertAttempts(HttpStatusCode status, int expected)
    {
        await using var f = new KubeTenureFixture();
        f.Replace = (_, _) => Task.FromException<V1Lease>(KubeTenureFixture.Status(status));
        await using var provider = f.CreateProvider(3);
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.AcquisitionFailed += (_, _) => failed.TrySetResult();
        await provider.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.OwnershipWrites.ShouldBe(expected); provider.CurrentLeadership.ShouldBeNull();
        await provider.StopAsync(CancellationToken.None);
    }
    [Fact]
    public async Task AttemptAnAbsentLeaseReadExactlyOnceBeforeCreatingIt()
    {
        await using var f = new KubeTenureFixture(); var reads = 0;
        f.Read = _ => Interlocked.Increment(ref reads) == 1 ? Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.NotFound)) : Task.FromResult(f.Snapshot());
        await f.StartAsync(); f.Creates.ShouldBe(1); reads.ShouldBe(2); f.OwnershipWrites.ShouldBe(1);
    }
    [Fact]
    public async Task StillRetryATransientFailureAndThenSucceed()
    {
        await using var f = new KubeTenureFixture(); var attempts = 0;
        f.Replace = (body, _) => Interlocked.Increment(ref attempts) == 1 ? Task.FromException<V1Lease>(KubeTenureFixture.Status(HttpStatusCode.ServiceUnavailable)) : Task.FromResult(f.Persist(body));
        await using var provider = f.CreateProvider(3);
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.BecameLeader += (_, _) => acquired.TrySetResult();
        await provider.StartAsync(CancellationToken.None); await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        attempts.ShouldBe(2); provider.CurrentLeadership.ShouldNotBeNull();
    }
}
