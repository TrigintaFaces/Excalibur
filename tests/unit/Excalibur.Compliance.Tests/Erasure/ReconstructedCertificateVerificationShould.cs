// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds what a RECONSTRUCTED erasure certificate is allowed to attest.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reconstruction path.</b> A certificate is normally built and persisted on the completion path,
/// with the real deleted-key ids. When no certificate can be retrieved, the service rebuilds one from the
/// stored status alone — and that status carries the key-deletion COUNT but never the key ids. The
/// rebuild therefore cannot substantiate a key deletion, which the code says plainly, and it sets
/// <c>Verified</c> from the count: true when the count is zero, on the reasoning that there was then
/// nothing to substantiate.
/// </para>
/// <para>
/// <b>Why zero cannot carry that decision.</b> For a key-destruction erasure, zero keys deleted does not
/// mean "nothing needed shredding" — it means <i>nothing was shredded</i>. The certificate then attests a
/// verified erasure where no erasure occurred. The guarding comment beside it refuses "verified with
/// empty key ids WHEN KEYS WERE DELETED"; it does not refuse "verified when nothing was deleted at all",
/// and that is the gap.
/// </para>
/// <para>
/// <b>Who reads the result.</b> This certificate is the artifact a consumer hands to a regulator or an
/// auditor to evidence a completed erasure. An attestation of verification over an erasure that
/// substantiated nothing is not a wrong field value; it is a false statement made in the consumer's name.
/// </para>
/// <para>
/// <b>The root cause is the codomain, not the comparison.</b> <c>Verified</c> is a
/// <see langword="bool" />, so it has nowhere to put "there was nothing to verify". Every producer is
/// forced to pick one of the two available values and both are assertions. A third state would make the
/// vacuous pass inexpressible rather than merely commented against; until it exists, the arms below pin
/// the behaviour that must not be reported.
/// </para>
/// <para>
/// <b>Scope, stated because a wider claim was measured and refused.</b> This path is NOT reachable for a
/// failed or partially completed erasure: the method refuses any status other than completed, and the
/// arms below pin that refusal so it cannot be relaxed. The reachable shape is a COMPLETED erasure whose
/// certificate cannot be retrieved — a consumer-supplied certificate store that returns nothing, a
/// retention policy that removed it, or a reader pointed at a different store — combined with a
/// key-deletion count of zero, which is ordinary for an erasure discharged entirely by record deletion.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ReconstructedCertificateVerificationShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();
	private readonly ErasureService _sut;

	public ReconstructedCertificateVerificationShould()
	{
		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore))).Returns(_certStore);

		// No retrievable certificate: this is exactly the condition that selects the reconstruction path.
		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));

		_sut = new ErasureService(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventoryService,
			null,
			TestAnnotationSource.None,
			[]);
	}

	/// <summary>
	/// SAFETY, and the one that is RED at the time of writing. A count of zero substantiates nothing.
	/// </summary>
	/// <remarks>
	/// The reconstruction holds no deleted-key ids by construction, so it can substantiate nothing
	/// whatever the count says. Zero keys deleted is not "nothing needed doing" — for a key-destruction
	/// erasure it is the statement that no key was shredded.
	/// </remarks>
	[Fact]
	public async Task Refuse_to_attest_verification_from_a_count_it_cannot_substantiate()
	{
		var requestId = Guid.NewGuid();
		GivenStoredStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 0);

		var certificate = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		certificate.Payload.Verification.Verified.ShouldBeFalse(
			"the reconstruction path holds no deleted-key ids, so it cannot substantiate a key deletion "
			+ "whatever the count says. A zero-key erasure is not a verified erasure; it is one where "
			+ "nothing was shredded -- and this certificate is what a consumer hands to a regulator.");
	}

	/// <summary>
	/// SAFETY. A non-zero count is already refused, and must stay refused.
	/// </summary>
	/// <remarks>
	/// This half is correct today. It is pinned so that a fix for the zero case cannot be written by
	/// loosening this one — the two are set by a single expression, so an edit reaches both.
	/// </remarks>
	[Fact]
	public async Task Keep_refusing_verification_when_keys_were_deleted_but_cannot_be_named()
	{
		var requestId = Guid.NewGuid();
		GivenStoredStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 3);

		var certificate = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		certificate.Payload.Verification.Verified.ShouldBeFalse(
			"three keys were deleted and this path cannot name any of them, so the attestation would be "
			+ "unsubstantiated.");
	}

	/// <summary>
	/// SAFETY. A certificate is issued only for a COMPLETED erasure.
	/// </summary>
	/// <remarks>
	/// This refusal is what bounds the defect above, and it is the reason a wider claim about failed
	/// erasures does not hold: a failed or partially completed request never reaches the reconstruction
	/// at all. Pinning it here keeps that bound from being relaxed without anyone noticing that it was
	/// the thing making the narrower defect narrow.
	/// </remarks>
	[Theory]
	[InlineData(ErasureRequestStatus.Failed)]
	[InlineData(ErasureRequestStatus.PartiallyCompleted)]
	[InlineData(ErasureRequestStatus.Scheduled)]
	[InlineData(ErasureRequestStatus.InProgress)]
	public async Task Refuse_to_issue_any_certificate_for_an_erasure_that_did_not_complete(
		ErasureRequestStatus status)
	{
		var requestId = Guid.NewGuid();
		GivenStoredStatus(requestId, status, keysDeleted: null);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.GenerateCertificateAsync(requestId, CancellationToken.None)).ConfigureAwait(false);
	}

	/// <summary>
	/// LIVENESS, and it is the arm that stops the safety arms from passing vacuously.
	/// </summary>
	/// <remarks>
	/// A reconstruction that threw for everything, or reported <c>Verified = false</c> by refusing to
	/// produce a certificate at all, would satisfy every safety arm above and be indistinguishable from a
	/// correct one. It must still produce a certificate for a completed erasure, still carry the request
	/// identity, and still report the deleted-key ids as empty rather than fabricating any.
	/// </remarks>
	[Fact]
	public async Task Still_produce_a_certificate_that_reports_what_it_does_know()
	{
		var requestId = Guid.NewGuid();
		GivenStoredStatus(requestId, ErasureRequestStatus.Completed, keysDeleted: 2);

		var certificate = await _sut.GenerateCertificateAsync(requestId, CancellationToken.None)
			.ConfigureAwait(false);

		certificate.Payload.RequestId.ShouldBe(requestId);
		certificate.Payload.CertificateId.ShouldNotBe(Guid.Empty);
		certificate.Payload.Summary.KeysDeleted.ShouldBe(2);
		certificate.Payload.Verification.DeletedKeyIds.ShouldBeEmpty(
			"this path has no key ids, and an empty list says so. Fabricating one would be the dishonest "
			+ "attestation the reconstruction exists to avoid.");
	}

	private void GivenStoredStatus(Guid requestId, ErasureRequestStatus status, int? keysDeleted)
	{
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(new ErasureStatus
			{
				RequestId = requestId,
				Status = status,
				DataSubjectIdHash = "subject-hash",
				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.DataSubjectRequest,
				RequestedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
				CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
				RequestedBy = "operator",
				UpdatedAt = DateTimeOffset.UtcNow,
				KeysDeleted = keysDeleted,
			}));
	}
}
