// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds what <c>VerificationSummary.Methods</c> is allowed to say on an issued certificate.
/// </summary>
/// <remarks>
/// <para>
/// <b>The configured list and the performed check are different facts.</b>
/// <c>ErasureOptions.VerificationMethods</c> says which checks this deployment is set up to run. Naming it
/// on a certificate asserts those checks were run <i>against this erasure</i> — which the configuration
/// cannot know. The live path substantiates by destroying keys through the key-management admin and
/// collecting their ids; that, and only that, is what it may name.
/// </para>
/// <para>
/// The sibling already does this correctly: <c>ErasureVerificationService</c> accumulates a
/// <c>methodsUsed</c> value from the branches that actually executed and reports that. This arm holds the
/// certificate path to the same rule.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CertificateNamesTheMethodThatRanShould
{
	private readonly IErasureStore _store = A.Fake<IErasureStore>();
	private readonly IErasureCertificateStore _certStore = A.Fake<IErasureCertificateStore>();
	private readonly IKeyManagementAdmin _keyAdmin = A.Fake<IKeyManagementAdmin>();
	private readonly ILegalHoldService _legalHoldService = A.Fake<ILegalHoldService>();
	private readonly IDataInventoryService _dataInventoryService = A.Fake<IDataInventoryService>();

	// Deliberately NOT the method the live path can perform, and deliberately two flags, so a certificate
	// that echoed configuration would be unmistakable in the failure message.
	private const VerificationMethod ConfiguredButNeverRun =
		VerificationMethod.AuditLog | VerificationMethod.DecryptionFailure;

	// SAFETY. A certificate must not name a check because it was configured.
	[Fact]
	public async Task Not_name_a_configured_method_that_never_ran()
	{
		var certificate = await ExecuteAndCaptureCertificateAsync(keyDestructionSucceeds: true)
			.ConfigureAwait(false);

		certificate.Payload.Verification.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeFalse(
			"AuditLog is configured on this host and was never performed for this erasure; a certificate "
			+ "that names it asserts a check that did not happen, and the signature now covers the claim.");

		certificate.Payload.Verification.Methods.HasFlag(VerificationMethod.DecryptionFailure)
			.ShouldBeFalse();
	}

	// LIVENESS. Without this, "always None" satisfies the arm above while telling a regulator nothing.
	// A real key destruction MUST be named.
	[Fact]
	public async Task Name_key_management_when_keys_were_actually_destroyed()
	{
		var certificate = await ExecuteAndCaptureCertificateAsync(keyDestructionSucceeds: true)
			.ConfigureAwait(false);

		certificate.Payload.Verification.Methods.ShouldBe(
			VerificationMethod.KeyManagementSystem,
			"key destruction through the key-management admin is what actually substantiated this "
			+ "erasure, so it is what the certificate must name.");
	}

	// SAFETY. Nothing substantiated => the enum's own word for it, not a borrowed one.
	[Fact]
	public async Task Say_none_when_nothing_was_substantiated()
	{
		var certificate = await ExecuteAndCaptureCertificateAsync(keyDestructionSucceeds: false)
			.ConfigureAwait(false);

		certificate.Payload.Verification.Methods.ShouldBe(
			VerificationMethod.None,
			"no key was destroyed, so no verification method was performed; None is the enum's own "
			+ "word for that and is the only honest value.");
	}

	private async Task<ErasureCertificate> ExecuteAndCaptureCertificateAsync(bool keyDestructionSucceeds)
	{
		var requestId = Guid.NewGuid();

		A.CallTo(() => _store.GetService(typeof(IErasureCertificateStore))).Returns(_certStore);
		A.CallTo(() => _store.GetStatusAsync(requestId, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(new ErasureStatus
			{
				RequestId = requestId,
				DataSubjectIdHash = "abc123hash",
				IdType = DataSubjectIdType.UserId,
				Scope = ErasureScope.User,
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				Status = ErasureRequestStatus.Scheduled,
				RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
				RequestedBy = "admin",
				UpdatedAt = DateTimeOffset.UtcNow,
			}));
		A.CallTo(() => _store.UpdateStatusAsync(
				requestId, A<ErasureRequestStatus>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		A.CallTo(() => _legalHoldService.CheckHoldsAsync(
				A<string>._, A<DataSubjectIdType>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new LegalHoldCheckResult { HasActiveHolds = false, ActiveHolds = [] }));

		// Completed is the ONLY destruction state that may be attested as erased; NotFound collects no id,
		// which is the "substantiated nothing" arm.
		A.CallTo(() => _keyAdmin.DeleteKeyAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult(keyDestructionSucceeds
				? KeyDestructionOutcome.CompletedAt(DateTimeOffset.UtcNow)
				: KeyDestructionOutcome.NotFound));

		ErasureCertificate? saved = null;
		A.CallTo(() => _certStore.SaveCertificateAsync(A<ErasureCertificate>._, A<CancellationToken>._))
			.Invokes((ErasureCertificate c, CancellationToken _) => saved = c)
			.Returns(Task.CompletedTask);
		A.CallTo(() => _certStore.GetCertificateAsync(A<Guid>._, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureCertificate?>(null));

		var sut = new ErasureService(
			_store,
			_keyAdmin,
			Microsoft.Extensions.Options.Options.Create(new ErasureOptions
			{
				// The host declares checks it is configured for. The certificate must not inherit them.
				VerificationMethods = ConfiguredButNeverRun,
				KeyShredOnlyErasure = true,
				Retention = new ErasureRetentionOptions { SigningKey = new byte[32] },
			}),
			NullLogger<ErasureService>.Instance,
			TestDataSubjectHasher.Instance,
			_legalHoldService,
			_dataInventoryService,
			null,
			TestAnnotationSource.None,
			null);

		_ = await sut.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		return saved.ShouldNotBeNull(
			"the execution must have issued a certificate, or these arms assert nothing");
	}
}
