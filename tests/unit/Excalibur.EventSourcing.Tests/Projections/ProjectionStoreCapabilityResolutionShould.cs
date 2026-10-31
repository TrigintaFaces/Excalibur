// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Binds the capability resolution that <see cref="IProjectionStore{TProjection}"/> supplies by default.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this guards.</b> A store implementing BOTH apply-path contracts cannot answer the capability
/// query from a type test: their preconditions are mutually exclusive — a transactional row carries the
/// unnumbered sentinel and the writing source's identity, a positioned row carries a real stream position
/// — so which one an instance honours depends on the destination it addresses, which only the
/// implementation knows. The default therefore REFUSES the ambiguous query, obliging such a type to
/// override and declare.
/// </para>
/// <para>
/// <b>Why this test and not an architecture census.</b> I first wrote this as a reflection guard asserting
/// that a both-implementing type must override the resolution. That assertion was true only BEFORE the
/// default was fixed; once the default refuses, inheriting it is correct, and the guard was asserting a
/// requirement that no longer held. The property that matters is behavioural — what the resolution
/// RETURNS — and that is what this binds.
/// </para>
/// <para>
/// <b>The fixtures implement the interfaces from scratch</b>, inheriting no first-party base, so the
/// assertions bind the INTERFACE's own default rather than re-testing a convenience base class that
/// happens to supply the member.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class ProjectionStoreCapabilityResolutionShould
{
    /// <summary>SAFETY: a store declaring both apply paths offers neither through the default.</summary>
    [Fact]
    public void Refuse_both_apply_paths_when_a_store_declares_both_and_does_not_override()
    {
        IProjectionStore<Model> store = new AmbiguousStore();

        store.GetService(typeof(IPositionedProjectionStore<Model>)).ShouldBeNull(
            "a type declaring both apply paths must not offer the positioned contract from a type test: "
            + "which path an instance owns is decided by its destination, not by its declaration");

        store.GetService(typeof(ITransactionalProjectionStore<Model>)).ShouldBeNull(
            "and it must not offer the transactional contract either -- the refusal is symmetric, so the "
            + "ambiguity is resolved by the implementation declaring a destination kind, never by the "
            + "order a caller happens to ask in");
    }

    /// <summary>
    /// LIVENESS: a POSITIONED-ONLY store is unambiguous and still answers, so the refusal above is not
    /// satisfied by a default that declines everything.
    /// </summary>
    /// <remarks>
    /// This is the arm that keeps the fix scoped to the defect. Every document-store provider is
    /// positioned-only and relies on this default; a blanket refusal would have removed their capability
    /// and no safety assertion above would have noticed.
    /// </remarks>
    [Fact]
    public void Still_offer_the_positioned_contract_for_a_store_that_declares_only_that_path()
    {
        IProjectionStore<Model> store = new PositionedOnlyStore();

        store.GetService(typeof(IPositionedProjectionStore<Model>)).ShouldNotBeNull(
            "a positioned-only store is unambiguous: the type test answers correctly and must keep doing so");

        store.GetService(typeof(ITransactionalProjectionStore<Model>)).ShouldBeNull(
            "and it still declines a contract it does not implement at all");
    }

    private sealed class Model
    {
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>Declares both apply paths and supplies no capability resolution of its own.</summary>
    private sealed class AmbiguousStore : IProjectionStore<Model>, IPositionedProjectionStore<Model>,
        ITransactionalProjectionStore<Model>
    {
        public Task<Model?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<Model?>(null);

        public Task UpsertAsync(string id, Model projection, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Model>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Model>>([]);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
            Task.FromResult(0L);

        public Task<(Model? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id,
            CancellationToken cancellationToken) => Task.FromResult<(Model?, ProjectionPosition)>((null, default));

        public Task UpsertUnnumberedAsync(string id, Model projection, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(string id, Model projection,
            long? expectedPosition, long newPosition, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));

        public Task<ProjectionRefoldResult> RefoldAtPositionAsync(string id, Model projection, long atPosition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));

        public Task<ProjectionRebuildResult> RebuildAtPositionAsync(string id, Model projection, long newPosition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));

        public string Serialize(Model projection) => "{}";

        public string Serialize(Model projection, JsonTypeInfo<Model> typeInfo) => "{}";

        public ValueTask WriteAsync(ProjectionTransaction transaction, string aggregateId, string aggregateType,
            string json, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>Declares only the positioned path: the unambiguous case the default must still serve.</summary>
    private sealed class PositionedOnlyStore : IProjectionStore<Model>, IPositionedProjectionStore<Model>
    {
        public Task<Model?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<Model?>(null);

        public Task UpsertAsync(string id, Model projection, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Model>> QueryAsync(IDictionary<string, object>? filters, QueryOptions? options,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Model>>([]);

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
            Task.FromResult(0L);

        public Task<(Model? Projection, ProjectionPosition Position)> GetWithPositionAsync(string id,
            CancellationToken cancellationToken) => Task.FromResult<(Model?, ProjectionPosition)>((null, default));

        public Task UpsertUnnumberedAsync(string id, Model projection, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ProjectionAdvanceResult> UpsertAtPositionAsync(string id, Model projection,
            long? expectedPosition, long newPosition, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionAdvanceResult(ProjectionAdvanceOutcome.Applied, newPosition));

        public Task<ProjectionRefoldResult> RefoldAtPositionAsync(string id, Model projection, long atPosition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionRefoldResult(ProjectionRefoldOutcome.Applied, atPosition));

        public Task<ProjectionRebuildResult> RebuildAtPositionAsync(string id, Model projection, long newPosition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectionRebuildResult(ProjectionRebuildOutcome.Applied));
    }
}
