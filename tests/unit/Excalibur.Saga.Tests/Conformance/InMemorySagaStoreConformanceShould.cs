// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

using Excalibur.Saga.Orchestration;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Saga.Tests.Conformance;

/// <summary>
/// Conformance tests for the in-process <see cref="InMemorySagaStore"/> using the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract kit.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 851 / <c>qxatfw</c>: this is the FIRST concrete deriver of
/// <see cref="SagaStoreConformanceTestBase"/> — before it, the base's 15 contract facts had ZERO derivers
/// and executed against nothing (dead-contract / false confidence). Wiring the in-memory provider makes the
/// save/load round-trip, type-isolation, and concurrent save/load invariants actually run in unit CI. It is
/// prioritized because the saga subsystem has an open lost-update defect (<c>wh1skm</c>): the contract's
/// concurrent-update facts encode the very last-write-wins / consistency invariants at issue.
/// </para>
/// <para>
/// <see cref="InMemorySagaStore"/> is <c>internal</c> to <c>Excalibur.Saga</c>; this project has
/// <c>InternalsVisibleTo</c> access (Excalibur.Saga.csproj), so the real shipped in-memory provider is
/// exercised — not a test double. A fresh store per test-class instance gives xUnit per-method isolation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class InMemorySagaStoreConformanceShould : SagaStoreConformanceTestBase
{
	/// <inheritdoc/>
	/// <remarks>
	/// e1tsq2 (S853, NARROW pull-in): <see cref="InMemorySagaStore"/> now enforces optimistic concurrency
	/// (atomic version-gated CAS, store-owns-increment, throws <see cref="ConcurrencyException"/> on a stale
	/// save — bd-boxiyl folded in). So the in-memory provider is held to
	/// <see cref="SagaStoreConformanceTestBase.StaleSave_ThrowsConcurrencyException_NoLostUpdate"/>.
	/// </remarks>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override Task<ISagaStore> CreateStoreAsync() => Task.FromResult<ISagaStore>(new InMemorySagaStore());

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask;
}
