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

	public ErasureLifecycleHarness(
		IKeyManagementAdmin keyAdmin,
		IKeyManagementProvider keyProvider,
		IEnumerable<IErasureContributor>? contributors = null,
		bool withVerifier = true)
	{
		Store = new InMemoryErasureStore(
			TestDataSubjectHasher.Instance,
			UntenantedContext.Instance,
			Options.Create(new TenantContextOptions { RequireTenant = false }));

		A.CallTo(() => _legalHolds.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		var options = Options.Create(new ErasureOptions
		{
			KeyShredOnlyErasure = true,
			Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
		});

		Service = new ErasureService(
			Store,
			keyAdmin,
			options,
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHolds,
			null,
			null,
			TestAnnotationSource.None,
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

	public InMemoryErasureStore Store { get; }

	public ErasureService Service { get; }

	public IErasureVerificationService? Verifier { get; }

	public ErasureCompletionProcessor Processor { get; }

	/// <summary>Submits an erasure request for <paramref name="subjectId"/> and executes it.</summary>
	/// <returns>The request id and the key id of the subject's crypto-shred key.</returns>
	public async Task<(Guid RequestId, string SubjectKeyId, ErasureExecutionResult Result)> SubmitAndExecuteAsync(string subjectId)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = subjectId,
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test",
		};

		_ = await Service.RequestErasureAsync(request, CancellationToken.None);
		var result = await Service.ExecuteAsync(request.RequestId, CancellationToken.None);
		return (request.RequestId, TestDataSubjectHasher.Instance.HashDataSubjectId(subjectId), result);
	}

	public async Task<ErasureRequestStatus?> StatusOfAsync(Guid requestId) =>
		(await Store.GetStatusAsync(requestId, CancellationToken.None))?.Status;
}
