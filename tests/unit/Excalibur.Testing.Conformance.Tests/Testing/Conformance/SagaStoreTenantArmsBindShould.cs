// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Proves that the tenant-confinement arms in <see cref="SagaStoreConformanceTestKit"/> actually BIND --
/// that each goes RED against a store carrying the defect it names, and GREEN against one that does not.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISagaStore"/> states its confinement guarantee in falsifiable terms: a confined load "returns
/// the caller's own saga state and never another tenant's", and a confined save "can neither overwrite nor
/// be overwritten by another tenant's state for the same sagaId". Until these arms existed, nothing in the
/// kit asserted either half -- the guarantee was written and unenforced.
/// </para>
/// <para>
/// The fakes implement <see cref="ISagaStore"/> DIRECTLY, inheriting no first-party base, so the arms bind
/// the interface's own requirement rather than re-testing an inherited convenience.
/// </para>
/// <para>
/// The verdict matrix each test below pins, one cell at a time:
/// </para>
/// <code>
///                   LoadSafety   LoadLiveness   NoOverwrite   Untenanted
/// Partitioned       GREEN        GREEN          GREEN         GREEN
/// Blind             RED          green          RED           green
/// AnswersNothing    green        RED            RED           RED
/// </code>
/// <para>
/// The lower-case greens are load-bearing. A blind store passes the liveness arms -- it answers everyone,
/// including the right caller. A store that answers nothing passes the safety arm perfectly. Neither half
/// detects the other's defect, which is why both are mandatory.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaStoreTenantArmsBindShould
{
	#region Load confinement

	/// <summary>
	/// SAFETY-DETECTION: the confinement arm must FAIL against a store keyed without the tenant.
	/// </summary>
	[Fact]
	public async Task Red_LoadSafety_WhenTheStoreIsKeyedWithoutTheTenant()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunLoadSafetyArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"Tenant confinement violated",
			Case.Sensitive,
			"the arm must fail with the confinement diagnostic, not some incidental error");
	}

	/// <summary>LIVENESS: the same arm must PASS against a store that partitions by tenant.</summary>
	[Fact]
	public Task Green_LoadSafety_WhenTheStoreIsPartitionedByTenant() =>
		new ArmProbe(FakeMode.Partitioned).RunLoadSafetyArmAsync();

	/// <summary>
	/// Pins the safety arm's blind spot: an inert store satisfies it perfectly, which is why the liveness
	/// arm below cannot be dropped.
	/// </summary>
	[Fact]
	public Task Green_LoadSafety_EvenWhenTheStoreAnswersNothing() =>
		new ArmProbe(FakeMode.AnswersNothing).RunLoadSafetyArmAsync();

	/// <summary>
	/// LIVENESS-DETECTION: the own-saga arm must FAIL against a store that resolves nothing.
	/// </summary>
	/// <remarks>
	/// Confining by resolving nothing passes every safety assertion ever written. For a saga store the
	/// consequence is particularly quiet: every business process silently restarts from nothing on each
	/// load, with no error raised anywhere.
	/// </remarks>
	[Fact]
	public async Task Red_LoadLiveness_WhenTheStoreAnswersNothing()
	{
		var probe = new ArmProbe(FakeMode.AnswersNothing);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunLoadLivenessArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("confinement is inert", Case.Sensitive);
	}

	/// <summary>LIVENESS: the same arm must PASS when the store resolves its own saga.</summary>
	[Fact]
	public Task Green_LoadLiveness_WhenTheStoreResolvesItsOwnSaga() =>
		new ArmProbe(FakeMode.Partitioned).RunLoadLivenessArmAsync();

	#endregion

	#region Save confinement

	/// <summary>
	/// SAVE-DETECTION: the no-overwrite arm must FAIL against a store whose WRITES are keyed on the saga
	/// identifier alone.
	/// </summary>
	/// <remarks>
	/// This is the defect the load arms cannot see. A store may filter reads by tenant correctly and still
	/// key its writes on the identifier, at which point the second tenant's save destroys the first
	/// tenant's in-flight business process -- silently, with no error on either side.
	/// </remarks>
	[Fact]
	public async Task Red_NoOverwrite_WhenWritesAreKeyedOnTheSagaIdAlone()
	{
		var probe = new ArmProbe(FakeMode.Blind);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunNoOverwriteArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"OVERWROTE",
			Case.Sensitive,
			"the arm must name the overwrite -- that is the diagnosis a provider author acts on");
	}

	/// <summary>LIVENESS: the same arm must PASS when each partition holds its own state.</summary>
	[Fact]
	public Task Green_NoOverwrite_WhenEachPartitionHoldsItsOwnState() =>
		new ArmProbe(FakeMode.Partitioned).RunNoOverwriteArmAsync();

	#endregion

	#region Untenanted partition

	/// <summary>
	/// UNTENANTED-DETECTION: the arm must FAIL when the reserved partition resolves nothing.
	/// </summary>
	[Fact]
	public async Task Red_Untenanted_WhenTheReservedPartitionResolvesNothing()
	{
		var probe = new ArmProbe(FakeMode.AnswersNothing);

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			probe.RunUntenantedArmAsync).ConfigureAwait(false);

		thrown.Message.ShouldContain("untenanted partition", Case.Sensitive);
	}

	/// <summary>LIVENESS: the same arm must PASS when the untenanted partition round-trips.</summary>
	[Fact]
	public Task Green_Untenanted_WhenTheReservedPartitionRoundTrips() =>
		new ArmProbe(FakeMode.Partitioned).RunUntenantedArmAsync();

	#endregion

	#region Harness

	/// <summary>The single decision each fake varies.</summary>
	private enum FakeMode
	{
		/// <summary>Tenant is part of the saga key. The conformant shape.</summary>
		Partitioned,

		/// <summary>Keyed on the saga identifier alone -- reads AND writes.</summary>
		Blind,

		/// <summary>Partitioned writes, but every load resolves nothing.</summary>
		AnswersNothing,
	}

	/// <summary>
	/// Drives the real kit arms against a supplied fake. Subclassing is the only way in: calling the arms
	/// THROUGH the kit is the point -- a reimplemented copy would prove things about the copy.
	/// </summary>
	private sealed class ArmProbe(FakeMode mode) : SagaStoreConformanceTestKit
	{
		// ONE backing set shared by every store this probe hands out, so that even a kit which called
		// CreateStore more than once could not satisfy a confinement arm by instance separation.
		private readonly ConcurrentDictionary<string, SagaState> _sagas = new(StringComparer.Ordinal);

		// Registered through a real container, exactly as a provider's own extension would: the fake is
		// resolved rather than handed over, so this harness exercises the same resolution path the arms
		// use against real providers.
		protected override void ConfigureProvider(IServiceCollection services) =>
			services.AddSingleton<ISagaStore>(
				sp => new FakeSagaStore(mode, sp.GetRequiredService<ITenantContext>(), _sagas));

		public Task RunLoadSafetyArmAsync() => TenantScopedLoad_MustNotSeeAnotherTenantsSaga();

		public Task RunLoadLivenessArmAsync() => TenantScopedLoad_MustSeeItsOwnSaga();

		public Task RunNoOverwriteArmAsync() =>
			TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();

		public Task RunUntenantedArmAsync() => UntenantedPartition_MustRoundTripItsOwnSaga();
	}

	/// <summary>A minimal saga store whose partitioning decision is fixed by construction.</summary>
	private sealed class FakeSagaStore(
		FakeMode mode,
		ITenantContext tenantContext,
		ConcurrentDictionary<string, SagaState> sagas) : ISagaStore
	{
		private string Tenant => tenantContext.TenantId ?? TenantScope.UntenantedSentinel;

		/// <summary>
		/// THE ONE EXPRESSION UNDER EXPERIMENT. Under <see cref="FakeMode.Blind"/> the tenant is absent from
		/// the key, so both partitions address the same slot -- one tenant's save overwrites the other's.
		/// </summary>
		private string KeyFor(Guid sagaId) =>
			mode == FakeMode.Blind ? sagaId.ToString("N") : $"{Tenant}|{sagaId:N}";

		public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
			where TSagaState : SagaState
		{
			if (mode == FakeMode.AnswersNothing)
			{
				return Task.FromResult<TSagaState?>(null);
			}

			return Task.FromResult(
				sagas.TryGetValue(KeyFor(sagaId), out var state) ? state as TSagaState : null);
		}

		public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
			where TSagaState : SagaState
		{
			ArgumentNullException.ThrowIfNull(sagaState);

			sagas[KeyFor(sagaState.SagaId)] = sagaState;

			return Task.CompletedTask;
		}
	}

	#endregion
}
