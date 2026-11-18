// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the window between the legal-hold check and each IRREVERSIBLE key destruction.
/// </summary>
/// <remarks>
/// <para>
/// <b>The hold was checked once per run, and the first destruction happens minutes later.</b> The
/// run-level check sits immediately after the atomic InProgress claim, and its own comment concedes it only
/// "tightens the window". Between it and the first <c>DeleteKeyAsync</c> runs the whole key-discovery pass.
/// A legal hold placed in that interval was never observed, and the keys were destroyed anyway —
/// irreversibly. That is spoliation of the data a hold exists to preserve, against a guarantee
/// <c>ARCHITECTURE.md</c> states without qualification.
/// </para>
/// <para>
/// <b>What the fix can and cannot do, because the difference is the property this arm binds.</b> The hold
/// lives in our store; the destruction happens at an external KMS that cannot roll back. So SOME window is
/// irreducible and this arm does not pretend otherwise. What is now guaranteed is that the window is
/// bounded by a SINGLE key rather than the entire discovery pass, that no FURTHER key is destroyed once a
/// hold is observed, and that the conflict is LOUD — the request cannot reach <c>Completed</c>, and the
/// count of keys already destroyed is recorded for an auditor. A silent irreversible violation becomes a
/// bounded and recorded one, which is the discriminator that matters: catastrophic is about SILENCE.
/// </para>
/// <para>
/// <b>The hold arrives by the SEAM the service reads, not by a bypass.</b> A sibling arm in this suite
/// places its "hold" by writing <c>BlockedByLegalHold</c> straight to the erasure store, which is not what
/// placing a hold does — <c>LegalHoldService.CreateHoldAsync</c> writes the hold store and takes no
/// dependency on <c>IErasureStore</c>. Here the hold becomes active through <c>CheckHoldsAsync</c>, which is
/// precisely the input the guard under test consumes.
/// </para>
/// <para>
/// <b>RED input.</b> Remove the per-key re-check from <c>ExecuteKeyDeletionsAsync</c> and BOTH keys are
/// destroyed, so the first assertion fails with 2 instead of 1. The hold is keyed on how many keys have
/// actually been destroyed rather than on a count of check calls, so the arm does not depend on how many
/// times the service happens to consult the hold store.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AHoldArrivingBetweenKeyDestructionsStopsTheNextOneShould
{
	// Reports a GENERATION, which erasure reads before destroying. A bare fake cannot answer that
	// lookup, and the service now ABORTS the destruction rather than destroying something it could never
	// record -- so a bare fake here destroys nothing at all and every arm fails for the wrong reason.
	// Every shipped key provider implements IKeyManagementProvider alongside the admin, which is what
	// this models.
	private readonly IKeyManagementAdmin _keyAdmin =
		KeyDestructionFakes.AdminThatReportsAGenerationForEveryKey();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventory = A.Fake<IDataInventoryService>();

	/// <summary>
	/// SAFETY. A hold that becomes active after the first key is destroyed must stop the second, and the
	/// request must not be attested Completed.
	/// </summary>
	[Fact]
	public async Task Destroy_no_further_key_once_a_hold_becomes_active_mid_pass()
	{
		var store = new InMemoryErasureStore(
			TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()), TimeProvider.System);
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var destroyed = new List<string>();

		// Two handles, so "stopped before the next one" is observable at all. One key could not
		// distinguish "stopped" from "there was nothing left to do".
		A.CallTo(() => _dataInventory.DiscoverAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DataInventory
			{
				DataSubjectId = "user-hold-mid-destruction",
				AssociatedKeys =
				[
					new KeyReference { KeyId = "subject-key-1", KeyScope = EncryptionKeyScope.User },
					new KeyReference { KeyId = "subject-key-2", KeyScope = EncryptionKeyScope.User },
				],
			}));

		// THE HOLD ARRIVES AFTER THE FIRST DESTRUCTION. Keyed on destroyed.Count rather than on a call
		// index: the service may consult the hold store a different number of times than this arm assumes,
		// and an arm that breaks when it does would be measuring the implementation instead of the property.
		// CheckHoldsByHashAsync, not CheckHoldsAsync: the execute-time checks take the hash-accepting member,
		// because the only identifier a running erasure holds is the hash. Configuring the raw overload here
		// would leave the guard reading an unconfigured fake, which is how this arm first passed a build where
		// the production call site had already moved.
		A.CallTo(() => _legalHoldService.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(new LegalHoldCheckResult
			{
				HasActiveHolds = destroyed.Count >= 1,
				ActiveHolds = [],
			}));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, int _, CancellationToken _) =>
			{
				destroyed.Add(keyId);
				return Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
			});

		var sut = BuildService(store);

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		destroyed.Count.ShouldBe(
			1,
			customMessage: "a legal hold that became active after the first key was destroyed must stop the "
			+ "destruction of every later key. Destruction is irreversible, so a key destroyed after the hold "
			+ "exists is data the hold was placed to preserve and cannot be recovered. Destroyed: "
			+ string.Join(", ", destroyed));

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status.Status.ShouldNotBe(
			ErasureRequestStatus.Completed,
			customMessage: "a request that stopped on a legal hold must NOT be attested Completed. Completed "
			+ "carries a signed certificate, and a certificate over an erasure the law interrupted is a false "
			+ "compliance statement held by a regulated consumer.");
	}

	/// <summary>
	/// SAFETY, and the arm that could not pass before the hash-depth fix. A SUBJECT-SPECIFIC hold placed
	/// through the real service, after the request exists, must block the execution.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this arm exists.</b> <c>CheckHoldsAsync</c> hashes its first argument unconditionally, and the
	/// execute-time checks were handing it <c>status.DataSubjectIdHash</c>, which is already hashed. The query
	/// therefore ran for a DOUBLE hash against a store keyed on a single one, and HMAC-SHA256 is not
	/// idempotent, so it could never match. A subject-specific hold was invisible to every execute-time check
	/// and the erasure proceeded over it, irreversibly.
	/// </para>
	/// <para>
	/// <b>The hold is placed AFTER the request, deliberately.</b> The request-time check passes the RAW
	/// identifier and was always correct, so a hold that exists first is caught there and the execution path
	/// is never reached. Placing it afterwards is both the real-world case and the only way to isolate the
	/// execute-time check.
	/// </para>
	/// <para>
	/// <b>Real service, real store, no fake.</b> The hold goes in through <c>CreateHoldAsync</c> and is read
	/// back through the production query path, with the same hasher on both sides -- which is the whole point,
	/// since the defect was a disagreement about how many times that hasher had been applied.
	/// </para>
	/// <para>
	/// <b>RED input:</b> point the execute-time checks back at <c>CheckHoldsAsync</c> with
	/// <c>status.DataSubjectIdHash</c> and this arm fails -- the key is destroyed and the status is not
	/// <c>BlockedByLegalHold</c>.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Block_the_execution_when_a_subject_hold_is_placed_after_the_request_exists()
	{
		var tenantOptions = Options.Create(new TenantContextOptions());
		var holdStore = new InMemoryLegalHoldStore(UntenantedContext.Instance, tenantOptions);
		var holds = new LegalHoldService(
			holdStore, TestDataSubjectHasher.Instance, NullLogger<LegalHoldService>.Instance);

		var store = new InMemoryErasureStore(
			TestDataSubjectHasher.Instance, UntenantedContext.Instance, tenantOptions, TimeProvider.System);

		const string subject = "user-hold-placed-after-the-request";
		var request = new ErasureRequest
		{
			DataSubjectId = subject,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		// AFTER the request exists. See the remarks.
		_ = await holds.CreateHoldAsync(
			new LegalHoldRequest
			{
				DataSubjectId = subject,
				IdType = DataSubjectIdType.UserId,
				Basis = LegalHoldBasis.LegalObligation,
				CaseReference = "CASE-HOLD-AFTER-REQUEST",
				Description = "placed while the erasure request was already recorded",
				CreatedBy = "legal-counsel",
			},
			CancellationToken.None).ConfigureAwait(false);

		var destroyed = new List<string>();

		A.CallTo(() => _dataInventory.DiscoverAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DataInventory
			{
				DataSubjectId = subject,
				AssociatedKeys =
				[
					new KeyReference { KeyId = "held-subject-key", KeyScope = EncryptionKeyScope.User },
				],
			}));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, int _, CancellationToken _) =>
			{
				destroyed.Add(keyId);
				return Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
			});

		var sut = BuildServiceWith(store, holds);

		_ = await sut.ExecuteAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);

		destroyed.ShouldBeEmpty(
			customMessage: "a subject-specific legal hold must block the erasure before ANY key is destroyed. "
			+ "Destruction is irreversible, so a key destroyed here is data a court order said to keep. "
			+ "Destroyed: " + string.Join(", ", destroyed));

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None)
			.ConfigureAwait(false);
		status.ShouldNotBeNull();
		status.Status.ShouldBe(
			ErasureRequestStatus.BlockedByLegalHold,
			customMessage: "the request must record that a HOLD stopped it, not a failure. The two have "
			+ "different remedies and only one of them is a bug.");
	}

	/// <summary>
	/// SAFETY — THE TENANT TERM IN THE HOLD LOOKUP. A hold placed in a tenant must refuse that tenant's
	/// erasure, and the erasure must look for it under the tenant its own request recorded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why the untenanted arm above cannot establish this.</b> It runs with the reserved untenanted
	/// sentinel on both sides, so the hold's tenant and the erasure's tenant agree no matter which value the
	/// service passes. An erasure that looked the hold up under the AMBIENT tenant, or under no tenant at
	/// all, instead of under the tenant its request recorded, is therefore invisible to it — and that
	/// substitution finds no row for a hold that genuinely exists. A gate that is still present and simply
	/// finds no row is not a gate, and the destruction it fails to stop cannot be undone.
	/// </para>
	/// <para>
	/// <b>The hold is placed through the real service into the real store</b>, and read back through the
	/// production query path, for the same reason as the arm above: the property is an agreement about the
	/// persisted identity, and a fake asserts the agreement instead of testing it.
	/// </para>
	/// <para>
	/// <b>RED input:</b> pass a tenant other than the request's own recorded one to the execute-time
	/// <c>CheckHoldsByHashAsync</c> and this arm destroys the key under a live hold. Measured.
	/// </para>
	/// <para>
	/// <b>Passing NO tenant reddens NEITHER arm, and that is worth recording because it is the mutant a
	/// reader reaches for first.</b> Measured: with the term replaced by <see langword="null"/> at both
	/// execute-time call sites, all four arms in this suite stay GREEN. The hold query applies the AMBIENT
	/// tenant term unconditionally and treats the caller's argument as a further narrowing, so dropping the
	/// argument removes a narrowing rather than the scoping — it cannot widen the result past the ambient
	/// tenant. A substituted WRONG tenant is what defeats the lookup, which is why that is the mutant above.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Refuse_the_destruction_when_a_hold_exists_in_the_tenant_that_requested_the_erasure()
	{
		var (holds, store, tenant) = BuildTenantedStores();
		const string subject = "user-held-in-one-tenant";

		tenant.TenantId = HoldingTenant;
		var request = await SaveRequestAsync(store, subject).ConfigureAwait(false);
		await PlaceHoldAsync(holds, subject).ConfigureAwait(false);

		var destroyed = ArrangeOneDiscoverableKey(subject);

		_ = await BuildServiceWith(store, holds).ExecuteAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		destroyed.ShouldBeEmpty(
			customMessage: "a legal hold placed in the tenant that requested this erasure must stop it before "
			+ "ANY key is destroyed. A key destroyed here is data a court order said to keep, and the "
			+ "destruction is irreversible. Destroyed: " + string.Join(", ", destroyed));

		var status = await store.GetStatusAsync(request, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull().Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
	}

	/// <summary>
	/// LIVENESS — and the converse harm. One tenant's legal hold must not freeze a DIFFERENT tenant's
	/// erasure of a subject identifier the two happen to share.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a lookup that ignores its tenant argument and
	/// blocks on any hold anywhere — which converts an irreversible over-destruction into an indefinite
	/// refusal to discharge another tenant's erasure obligation. Consumer-supplied subject identifiers repeat
	/// across tenants, so that collision is ordinary rather than exotic.
	/// <para>
	/// <b>RED input:</b> remove both tenant terms from the subject-hold query in
	/// <c>InMemoryLegalHoldStore.GetActiveHoldsForDataSubjectAsync</c> — the unconditional ambient term and
	/// the caller's narrowing term — and this arm fails with the erasure blocked by a hold belonging to a
	/// tenant that never requested it. Measured. The safety arm above stays GREEN under that mutant.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Still_erase_for_a_tenant_whose_subject_is_held_only_in_another_tenant()
	{
		var (holds, store, tenant) = BuildTenantedStores();
		const string subject = "user-held-in-one-tenant";

		tenant.TenantId = HoldingTenant;
		await PlaceHoldAsync(holds, subject).ConfigureAwait(false);

		tenant.TenantId = ErasingTenant;
		var request = await SaveRequestAsync(store, subject).ConfigureAwait(false);

		var destroyed = ArrangeOneDiscoverableKey(subject);

		_ = await BuildServiceWith(store, holds).ExecuteAsync(request, CancellationToken.None)
			.ConfigureAwait(false);

		destroyed.ShouldNotBeEmpty(
			customMessage: "the hold belongs to a different tenant, so it must not block this one. An empty "
			+ "set here means a hold lookup that ignores its tenant argument, which leaves every tenant "
			+ "unable to discharge an erasure for any subject identifier another tenant has ever held");

		var status = await store.GetStatusAsync(request, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull().Status.ShouldNotBe(ErasureRequestStatus.BlockedByLegalHold);
	}

	private const string HoldingTenant = "tenant-with-the-hold";
	private const string ErasingTenant = "tenant-requesting-the-erasure";

	private static (LegalHoldService Holds, InMemoryErasureStore Store, MovableTenantContext Tenant)
		BuildTenantedStores()
	{
		var tenant = new MovableTenantContext();
		var tenantOptions = Options.Create(new TenantContextOptions { RequireTenant = true });

		return (
			new LegalHoldService(
				new InMemoryLegalHoldStore(tenant, tenantOptions),
				TestDataSubjectHasher.Instance,
				NullLogger<LegalHoldService>.Instance),
			new InMemoryErasureStore(TestDataSubjectHasher.Instance, tenant, tenantOptions, TimeProvider.System),
			tenant);
	}

	private static async Task<Guid> SaveRequestAsync(IErasureStore store, string subject)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = subject,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}

	private static Task PlaceHoldAsync(LegalHoldService holds, string subject) =>
		holds.CreateHoldAsync(
			new LegalHoldRequest
			{
				DataSubjectId = subject,
				IdType = DataSubjectIdType.UserId,
				Basis = LegalHoldBasis.LegalObligation,
				CaseReference = "CASE-TENANT-SCOPED-HOLD",
				Description = "placed in one tenant only",
				CreatedBy = "legal-counsel",
			},
			CancellationToken.None);

	private List<string> ArrangeOneDiscoverableKey(string subject)
	{
		var destroyed = new List<string>();

		A.CallTo(() => _dataInventory.DiscoverAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new DataInventory
			{
				DataSubjectId = subject,
				AssociatedKeys =
				[
					new KeyReference { KeyId = "held-subject-key", KeyScope = EncryptionKeyScope.User },
				],
			}));

		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((string keyId, int _, CancellationToken _) =>
			{
				destroyed.Add(keyId);
				return Task.FromResult(KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow));
			});

		return destroyed;
	}

	/// <summary>An ambient tenant the arms move between placing a hold and filing a request.</summary>
	private sealed class MovableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	private static async Task<Guid> ScheduleAsync(IErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-hold-mid-destruction",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}

	private ErasureService BuildServiceWith(IErasureStore store, ILegalHoldService holds) =>
		BuildCore(store, holds);

	private ErasureService BuildService(IErasureStore store) =>
		BuildCore(store, _legalHoldService);

	private ErasureService BuildCore(IErasureStore store, ILegalHoldService holds) =>
		new(
			store,
			_keyAdmin,
			Options.Create(new ErasureOptions
			{
				// Coverage is taken off the critical path by the documented opt-in rather than by a fixture
				// that fakes it: the subject here is the destruction loop, not the coverage gate.
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			holds,
			_dataInventory,
			null,
			TestAnnotationSource.None,
			TestRetentions.None,
			TestMintLedger.None,
			contributors: null);
}
