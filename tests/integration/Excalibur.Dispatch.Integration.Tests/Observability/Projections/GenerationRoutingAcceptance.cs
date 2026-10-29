// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Testing.Conformance;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

internal static class GenerationRoutingAcceptance
{
    internal static async Task VerifyAsync(IProjectionStore<ConformanceProjection> writer,
        IProjectionStore<ConformanceProjectionNextGeneration> next)
    {
        var token = CancellationToken.None;
        await writer.UpsertAsync("shared", new ConformanceProjection { Id = "shared", Total = 11 }, token);
        await next.UpsertAsync("shared", new ConformanceProjectionNextGeneration { Id = "shared", Total = 37 }, token);
        await next.UpsertAsync("next-only", new ConformanceProjectionNextGeneration { Id = "next-only", Total = 38 }, token);
        var connectionString = $"Data Source=generation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync(token);
        var services = new ServiceCollection();
        services.AddSqliteSubscriptionCheckpointStore(connectionString);
        await using var provider = services.BuildServiceProvider();
        var checkpoints = provider.GetRequiredService<ISubscriptionCheckpointStore>();
        var generations = (IProjectionGenerationStore)checkpoints;
        var roster = await checkpoints.BindSubscriptionAsync("stable", [await MemberAsync(writer, "A", 'A'), await MemberAsync(next, "B", 'B')], token);
        var first = roster.Members.Single(m => m.ProjectionKey == "A");
        var second = roster.Members.Single(m => m.ProjectionKey == "B");
        (await checkpoints.AdvanceCheckpointAsync("stable", "A", first.BindingId, null, 100, token)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
        (await generations.TrySetGenerationAsync("stable", "orders", null, "A", first.BindingId, 100, token)).ShouldBeTrue();
        var revision = (await generations.ReadGenerationAsync("stable", "orders", token))!.Revision;
        var reader = new ProjectionGenerationReader<ConformanceProjection>(checkpoints, writer, "stable", "orders", ["A", "B"]);
        var cutover = new ProjectionGenerationCutover(checkpoints, new FixedHead());
        var request = new ProjectionGenerationCutoverRequest
        {
            SubscriptionName = "stable", ReaderName = "orders", NewProjectionKey = "B",
            ExpectedRevision = revision, MaxAcceptableLag = 1000,
        };
        await AssertReadsAsync(reader, 11, 1);
        var absent = await cutover.CutOverAsync(request, token);
        absent.Outcome.ShouldBe(ProjectionCutoverOutcome.NewGenerationCursorAbsent);
        absent.NewGenerationCursor.ShouldBeNull();
        absent.Lag.ShouldBeNull();
        await AssertReadsAsync(reader, 11, 1);
        (await checkpoints.AdvanceCheckpointAsync("stable", "B", second.BindingId, null, 10, token)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
        request = request with { MaxAcceptableLag = 5 };
        (await cutover.CutOverAsync(request, token)).Outcome.ShouldBe(ProjectionCutoverOutcome.NewGenerationLagAboveThreshold);
        await AssertReadsAsync(reader, 11, 1);
        (await checkpoints.AdvanceCheckpointAsync("stable", "B", second.BindingId, 10, 98, token)).ShouldBe(CheckpointAdvanceOutcome.Advanced);
        var switched = await cutover.CutOverAsync(request, token);
        switched.Outcome.ShouldBe(ProjectionCutoverOutcome.CutOver);
        switched.Lag.ShouldBe(2);
        await AssertReadsAsync(reader, 37, 2);
        (await writer.GetByIdAsync("shared", token))!.Total.ShouldBe(11);
        (await writer.GetByIdAsync("next-only", token)).ShouldBeNull();
        (await cutover.CutOverAsync(request with { NewProjectionKey = "A" }, token)).Outcome.ShouldBe(ProjectionCutoverOutcome.Contested);
        await AssertReadsAsync(reader, 37, 2);
        var retained = await checkpoints.ReadSubscriptionAsync("stable", token);
        retained.Members.Single(m => m.ProjectionKey == "A").BindingId.ShouldBe(first.BindingId);
        (await checkpoints.ReleaseSubscriptionMemberAsync("stable", "B", second.BindingId, token)).ShouldBeFalse();
        // A fresh revision permits an intentional rollback, including all read surfaces.
        var active = (await generations.ReadGenerationAsync("stable", "orders", token))!;
        (await cutover.CutOverAsync(request with { NewProjectionKey = "A", ExpectedRevision = active.Revision }, token))
            .Outcome.ShouldBe(ProjectionCutoverOutcome.CutOver);
        await AssertReadsAsync(reader, 11, 1);
    }

    private static async Task<SubscriptionMember> MemberAsync<T>(IProjectionStore<T> store, string key, char fingerprint) where T : class
    {
        var identity = (IProjectionDestinationIdentity)store.GetService(typeof(IProjectionDestinationIdentity))!;
        var cluster = store.GetService(typeof(IAsyncProjectionDestinationScope)) is IAsyncProjectionDestinationScope asynchronous
            ? await asynchronous.GetScopeAsync(CancellationToken.None)
            : ((IProjectionDestinationScope)store.GetService(typeof(IProjectionDestinationScope))!).Scope;
        // Arrange the same binding metadata used by the host; scope encoding has separate adversarial tests.
        return new SubscriptionMember(key, new string(fingerprint, 64), identity.Identity)
        {
            DestinationScope = ProjectionTenantValidation.Scope(store, cluster),
        };
    }

    private static async Task AssertReadsAsync(ProjectionGenerationReader<ConformanceProjection> reader, int total, int count)
    {
        (await reader.GetByIdAsync("shared", CancellationToken.None))!.Total.ShouldBe(total);
        var rows = await reader.QueryAsync(null, null, CancellationToken.None);
        rows.Count.ShouldBe(count);
        rows.Single(row => row.Id == "shared").Total.ShouldBe(total);
        (await reader.CountAsync(null, CancellationToken.None)).ShouldBe(count);
    }

    private sealed class FixedHead : IGlobalStreamQuery
    {
        public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken) => ValueTask.FromResult(100L);
        public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(string eventType, GlobalStreamPosition position, int maxCount, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
