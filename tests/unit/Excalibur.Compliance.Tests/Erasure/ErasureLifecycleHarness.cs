// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Drives the REAL erasure lifecycle -- <see cref="ErasureService"/>, <see cref="InMemoryErasureStore"/>,
/// <see cref="ErasureVerificationService"/> and <see cref="ErasureCompletionProcessor"/> -- over a supplied key
/// store, so an arm can follow one request from submission to a terminal state against a given provider.
/// </summary>
/// <remarks>
/// Key-shred-only, with no data inventory: the per-subject key is the whole of the erasure, so the only thing
/// that decides the outcome is what the key store does with that key. That is exactly the variable these arms
/// exist to exercise.
/// </remarks>
internal sealed class ErasureLifecycleHarness
{
	private readonly ILegalHoldService _legalHolds = A.Fake<ILegalHoldService>();

	/// <summary>
	/// A single-tenant host's ambient context: it resolves the deployment's own default identity.
	/// </summary>
	/// <remarks>
	/// <b>This replaced the reserved UNTENANTED context, and the swap is the point rather than a tidy-up.</b>
	/// The fixture's requests name the resolved default identity, and the handle this harness derives is
	/// built from that same identity — but the context it used to supply resolved the *untenanted* sentinel,
	/// which is a different term. So the harness modelled a host where the write path and the erasure would
	/// have derived two different key handles, and it passed only because the store took the owner off the
	/// request instead of from the host. That is precisely the defect these arms now guard against: an
	/// erasure that destroys a handle nothing was ever written under reports a completed erasure having
	/// destroyed nothing. A real single-tenant host resolves one identity for both paths, which is what this
	/// context models, so every arm below now exercises agreement rather than depending on divergence.
	/// </remarks>
	private sealed class ResolvedDefaultTenantContext : ITenantContext
	{
		public static readonly ResolvedDefaultTenantContext Instance = new();

		public string? TenantId => TenantDefaults.DefaultTenantId;

		public bool HasTenant => true;
	}

	public ErasureLifecycleHarness(
		IKeyManagementAdmin keyAdmin,
		IKeyManagementProvider keyProvider,
		IEnumerable<IErasureContributor>? contributors = null,
		bool withVerifier = true,
		ITenantContext? tenantContext = null,
		bool requireTenant = false,
		ErasureOptions? erasureOptions = null,
		Func<InMemoryErasureStore, IErasureStore>? decorateStore = null)
	{
		Store = new InMemoryErasureStore(
			TestDataSubjectHasher.Instance,
			tenantContext ?? ResolvedDefaultTenantContext.Instance,
			Options.Create(new TenantContextOptions { RequireTenant = requireTenant }), TimeProvider.System);

		A.CallTo(() => _legalHolds.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		var options = Options.Create(erasureOptions ?? new ErasureOptions
		{
			KeyShredOnlyErasure = true,
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});

		Service = new ErasureService(
			decorateStore?.Invoke(Store) ?? Store,
			keyAdmin,
			options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHolds,
			null,
			null,
			TestAnnotationSource.None,
			TestRetentions.None, TestMintLedger.None,
			contributors);

		Verifier = withVerifier
			? new ErasureVerificationService(
				Store,
				keyProvider,
				A.Fake<IDataInventoryService>(),
				A.Fake<IAuditStore>(),
				options,
				NullLogger<ErasureVerificationService>.Instance)
			: null;

		Processor = new ErasureCompletionProcessor(
			Service,
			Store,
			Verifier,
			NullLogger<ErasureCompletionProcessor>.Instance);
	}

	/// <summary>
	/// The legal-hold authority the service asks, exposed so an arm can change its answer MID-LIFECYCLE.
	/// </summary>
	/// <remarks>
	/// A hold being placed and later released is a sequence, not a configuration, so an arm that binds the
	/// deferral and the resume end to end has to be able to answer differently on the second ask. Without
	/// this the harness can model a permanent hold or no hold, and neither is the case under test.
	/// </remarks>
	public ILegalHoldService LegalHolds => _legalHolds;

	public InMemoryErasureStore Store { get; }

	public ErasureService Service { get; }

	public IErasureVerificationService? Verifier { get; }

	public ErasureCompletionProcessor Processor { get; }

	/// <summary>Submits an erasure request for <paramref name="subjectId"/> and executes it.</summary>
	/// <returns>The request id and the key id of the subject's crypto-shred key.</returns>
	public async Task<(Guid RequestId, string SubjectKeyId, ErasureExecutionResult Result)> SubmitAndExecuteAsync(string subjectId)
	{
		// THE REQUEST NAMES NO TENANT, DELIBERATELY. It used to hardcode the resolved default identity,
		// which is wrong in every scope that is not that identity: under multi-tenancy these arms open a
		// scope for "tenant-t" and then filed a request claiming the default term instead. That passed only
		// because the store took the owner from the ambient scope and silently discarded the one on the
		// request -- so the fixture carried a contradiction that nothing could report. Leaving it unset lets
		// the host's resolved term be authoritative, which is what the write path encrypts under, and makes
		// this harness correct in both deployment modes rather than only the one it was written in.
		var request = new ErasureRequest
		{
			DataSubjectId = subjectId,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test",
		};

		_ = await Service.RequestErasureAsync(request, CancellationToken.None);

		// Read the owner the store ACTUALLY RECORDED, before executing, rather than recomputing it from a
		// constant. A constant is a second derivation of the same fact, and when the two disagree the
		// fixture asserts against a handle the product never used -- which is the defect class these arms
		// exist to catch, reproduced inside the test harness.
		var recorded = await Store.GetStatusAsync(request.RequestId, CancellationToken.None)
			?? throw new InvalidOperationException(
				"The erasure request was accepted but is not readable in this scope, so the harness cannot "
				+ "know which owner the store recorded.");

		var result = await Service.ExecuteAsync(request.RequestId, CancellationToken.None);

		var handle = SubjectKeyHandle.ForSubjectHash(
			new TenantId(recorded.TenantId!),
			TestDataSubjectHasher.Instance.HashDataSubjectId(subjectId),
			TestDataSubjectHasher.Instance);

		return (request.RequestId, handle.Value, result);
	}

	public async Task<ErasureRequestStatus?> StatusOfAsync(Guid requestId) =>
		(await Store.GetStatusAsync(requestId, CancellationToken.None))?.Status;
}
