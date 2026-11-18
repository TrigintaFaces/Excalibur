// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Dispatch;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// bd-412fo4 (S840, AC-7) — independent regression lock (author≠impl, TestsDeveloper).
/// <para>
/// Erasure verification must be NON-VACUOUS: a certificate that CLAIMS keys were deleted
/// (<c>KeysDeleted &gt; 0</c>) but carries NO confirmable <c>DeletedKeyIds</c> proves nothing and MUST
/// FAIL verification. The pre-fix code short-circuited <c>Success = true</c> on an empty key list, so a
/// cert could "pass" while asserting nothing — a false compliance signal. This lock is RED on the
/// pre-fix code and GREEN once verification records the failure (couples with the fot516 coverage gate,
/// same <c>ErasureService.cs</c> / <c>ErasureVerificationService.cs</c> edit).
/// </para>
/// <para>
/// AC-7b ("verification fails on any uncovered location") is satisfied STRUCTURALLY by the fot516 gate
/// (a Completed request — the only kind that gets a certificate — has zero uncovered locations; proven
/// by <c>ErasureCoverageGateShould</c>), so it needs no separate assertion here.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class NonVacuousErasureVerificationShould
{
    private readonly IErasureStore _erasureStore = A.Fake<IErasureStore>();
    private readonly IKeyManagementProvider _keyProvider = KeyDestructionFakes.ProviderThatReportsDestruction();
    private readonly IDataInventoryService _inventoryService = A.Fake<IDataInventoryService>();
    private readonly IAuditStore _auditStore = A.Fake<IAuditStore>();
    private readonly NullLogger<ErasureVerificationService> _logger = NullLogger<ErasureVerificationService>.Instance;

    [Fact]
    public async Task FailVerificationWhenCertClaimsKeysDeletedButListsNoDeletedKeyIds()
    {
        // Arrange — a Completed erasure whose certificate CLAIMS a key was deleted (KeysDeleted = 1) but
        // carries an EMPTY DeletedKeyIds list (the vacuous-claim case).
        var requestId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(CreateCompletedStatus(requestId, certificateId));

        var vacuousCertificate = CreateCertificateWithoutDeletedKeyIds(requestId, certificateId);
        var certificateStore = A.Fake<IErasureCertificateStore>();
        A.CallTo(() => certificateStore.GetCertificateByIdAsync(certificateId, A<CancellationToken>._))
            .Returns(vacuousCertificate);
        A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
            .Returns(certificateStore);

        // KMS confirms the key is gone — so the ONLY thing that can fail verification is the vacuous
        // KeysDeleted-with-no-key-IDs claim (non-vacuity: the failure is the 412fo4 check, not a KMS miss).
        _keyProvider.ReportsEveryKeyDestroyed();

        // KMS-only verification keeps the assertion isolated to the vacuous-keys check (no AuditLog setup needed).
        var sut = CreateService(new ErasureOptions { VerificationMethods = VerificationMethod.KeyManagementSystem });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert — a "KeysDeleted > 0 with no key IDs" certificate is non-verifiable. RED on pre-fix
        // (trivially passed on empty keys); GREEN once verification records the failure.
        result.Verified.ShouldBeFalse(
            "a certificate claiming KeysDeleted>0 with no confirmable DeletedKeyIds must NOT pass verification");
        result.Failures.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task PassVerificationWhenCertListsConfirmedDeletedKeyIds()
    {
        // Control — proves the gate keys on the PRESENCE of confirmable deleted key IDs, not a blanket
        // failure. A cert that lists the deleted key (confirmed gone by KMS) verifies. (GREEN both
        // pre- and post-fix; guards against a vacuous "always-false" lock.)
        var requestId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(CreateCompletedStatus(requestId, certificateId));

        var certificate = CreateCertificateWithDeletedKeyIds(requestId, certificateId, "key-1");
        var certificateStore = A.Fake<IErasureCertificateStore>();
        A.CallTo(() => certificateStore.GetCertificateByIdAsync(certificateId, A<CancellationToken>._))
            .Returns(certificate);
        A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
            .Returns(certificateStore);

        _keyProvider.ReportsDestroyed("key-1", true); // key confirmed destroyed by the provider

        var sut = CreateService(new ErasureOptions { VerificationMethods = VerificationMethod.KeyManagementSystem });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Verified.ShouldBeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // The verdict algebra: Verified is true ONLY when a method actually substantiated THIS execution
    // and nothing was recorded against it.
    //
    //     Verified  =>  methodsUsed != None  AND  failures is empty
    //
    // These three arms are deliberately distinct from the two above, which exercise a certificate
    // claiming KeysDeleted > 0 -- that path raises a CRITICAL failure, so it failed verification even
    // under the old severity-filtered predicate. These use KeysDeleted = 0, where the only record
    // produced is a WARNING: the state the old predicate waived.
    //
    // FIXTURE SCOPE, stated so a green here is not over-read: every arm runs on fakes. A fake cannot
    // exhibit a key-management backend's real "present but unconfirmable" answer. These arms bind the
    // VERDICT ALGEBRA, say nothing about destruction semantics, and discharge no rung of this seam's
    // rigor floor.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task RefuseVerificationWhenKeyDestructionWasNeverEvidencedEvenThoughAnotherMethodPassed()
    {
        // Arrange -- ARM A. A Completed erasure that deleted no keys, with a certificate carrying no
        // deleted-key IDs, so the vacuous-claim check does NOT fire (KeysDeleted = 0) and the only
        // record produced is the key-deletions WARNING. AuditLog is also enabled AND passes, so
        // methodsUsed is non-empty -- isolating this arm to the failures-is-empty half of the invariant.
        var requestId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(CreateCompletedStatusWithNoKeysDeleted(requestId, certificateId));
        SetupCertificate(certificateId, CreateCertificateClaimingNoKeys(requestId, certificateId));
        SetupAuditTrailWithCompletionEvent(requestId);

        var sut = CreateService(new ErasureOptions
        {
            VerificationMethods = VerificationMethod.KeyManagementSystem | VerificationMethod.AuditLog,
        });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert -- RED before the predicate fix: the warning was recorded in Failures and then waived,
        // because the verdict filtered on Critical severity only.
        result.Failures.ShouldNotBeEmpty(
            "the empty key set records a failure saying key destruction could not be verified");
        result.Verified.ShouldBeFalse(
            "a verdict carrying a record that destruction was never evidenced has established nothing");
        result.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue(
            "liveness: the audit method DID substantiate, so this arm cannot pass by refusing everything");
    }

    [Fact]
    public async Task ReportNoMethodsWhenTheOnlyConfiguredMethodCouldNotSubstantiate()
    {
        // Arrange -- ARM B. As Arm A but KMS is the ONLY configured method, so nothing substantiates.
        // This arm reddens if the predicate is ever re-widened to waive a severity again.
        var requestId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(CreateCompletedStatusWithNoKeysDeleted(requestId, certificateId));
        SetupCertificate(certificateId, CreateCertificateClaimingNoKeys(requestId, certificateId));

        var sut = CreateService(new ErasureOptions { VerificationMethods = VerificationMethod.KeyManagementSystem });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Methods.ShouldBe(
            VerificationMethod.None,
            "a method that could not substantiate must not be reported as having been used");
        result.Verified.ShouldBeFalse("verified by nothing is not verified");
    }

    [Fact]
    public async Task RefuseVerificationWhenNoVerificationMethodIsConfiguredAtAll()
    {
        // Arrange -- ARM C. The structural half, and the one a severity-predicate fix alone does NOT
        // close: VerificationMethods = None is a legal, unvalidated configuration. Every verification
        // branch is skipped, so Failures is EMPTY and nothing substantiated. A predicate keyed only on
        // "no failures were recorded" returns true here.
        var requestId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        A.CallTo(() => _erasureStore.GetStatusAsync(requestId, A<CancellationToken>._))
            .Returns(CreateCompletedStatusWithNoKeysDeleted(requestId, certificateId));

        // An UNCONFIGURED FakeItEasy IErasureStore returns a generic proxy from GetService, which the
        // service casts to IErasureCertificateStore and throws on -- producing a Critical failure and a
        // false RED that looks exactly like the one this arm is for. Return null explicitly: the arm is
        // about no method running, not about a certificate lookup.
        A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore))).Returns(null);

        var sut = CreateService(new ErasureOptions { VerificationMethods = VerificationMethod.None });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert -- this arm distinguishes the fix from the invariant. It is RED both before the fix and
        // after a failures-only predicate; it goes green only once the verdict also requires that some
        // method actually substantiated this execution.
        result.Failures.ShouldBeEmpty("nothing ran, so nothing was recorded against it");
        result.Methods.ShouldBe(VerificationMethod.None);
        result.Verified.ShouldBeFalse(
            "no method ran, so the framework established nothing and must not report otherwise");
    }

    private void SetupCertificate(Guid certificateId, ErasureCertificate certificate)
    {
        var certificateStore = A.Fake<IErasureCertificateStore>();
        A.CallTo(() => certificateStore.GetCertificateByIdAsync(certificateId, A<CancellationToken>._))
            .Returns(certificate);
        A.CallTo(() => _erasureStore.GetService(typeof(IErasureCertificateStore)))
            .Returns(certificateStore);
    }

    private void SetupAuditTrailWithCompletionEvent(Guid requestId)
    {
        var completionEvent = new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.Compliance,
            // The literal, because ErasureAuditActions is a PRIVATE nested class of the service under
            // test. A test cannot reference it, so the arm pins the string the service matches on.
            Action = "DataErasure.Completed",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "system",
            ResourceId = requestId.ToString(),
            ResourceType = "ErasureRequest",
        };

        A.CallTo(() => _auditStore.QueryAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns<IReadOnlyList<AuditEvent>>([completionEvent]);
    }

    private ErasureVerificationService CreateService(ErasureOptions options) =>
        new(
            _erasureStore,
            _keyProvider,
            _inventoryService,
            _auditStore,
            Microsoft.Extensions.Options.Options.Create(options),
            _logger);

    private static ErasureStatus CreateCompletedStatus(Guid requestId, Guid certificateId) =>
        new()
        {
        	TenantPartition = KeyedTenantPartition.Untenanted,
            RequestId = requestId,
            DataSubjectIdHash = "hash-abc123",
            IdType = DataSubjectIdType.UserId,
            Scope = ErasureScope.User,
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            Status = ErasureRequestStatus.Completed,
            RequestedBy = "test-admin",
            RequestedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            KeysDeleted = 1,
            CertificateId = certificateId,
        };

    private static ErasureStatus CreateCompletedStatusWithNoKeysDeleted(Guid requestId, Guid certificateId)
    {
        var status = CreateCompletedStatus(requestId, certificateId);
        return status with { KeysDeleted = 0 };
    }

    private static ErasureCertificate CreateCertificateClaimingNoKeys(Guid requestId, Guid certificateId)
    {
        var certificate = CreateCertificate(requestId, certificateId, deletedKeyIds: []);
        return certificate with
        {
            Payload = certificate.Payload with
            {
                Summary = certificate.Payload.Summary with { KeysDeleted = 0 },
            },
        };
    }

    private static ErasureCertificate CreateCertificateWithoutDeletedKeyIds(Guid requestId, Guid certificateId) =>
        CreateCertificate(requestId, certificateId, deletedKeyIds: []);

    private static ErasureCertificate CreateCertificateWithDeletedKeyIds(Guid requestId, Guid certificateId, params string[] keyIds) =>
        CreateCertificate(requestId, certificateId, deletedKeyIds: keyIds);

    private static ErasureCertificate CreateCertificate(Guid requestId, Guid certificateId, string[] deletedKeyIds) =>
        new()
        {
			Payload = new()
			{
				CertificateId = certificateId,
				RequestId = requestId,
				DataSubjectReference = "hash-abc123",
				RequestReceivedAt = DateTimeOffset.UtcNow.AddHours(-2),
				CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
				Method = ErasureMethod.CryptographicErasure,
				Summary = new ErasureSummary
            {
                KeysDeleted = 1, // CLAIMS a key was deleted
                RecordsAffected = 1,
                DataCategories = ["Operational"],
                TablesAffected = ["Orders"],
                DataSizeBytes = 128,
            },
				Verification = new VerificationSummary
            {
                Verified = true,
                Methods = VerificationMethod.KeyManagementSystem,
                VerifiedAt = DateTimeOffset.UtcNow,
                DeletedKeyIds = deletedKeyIds, // empty == vacuous claim
            },
				LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
				RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
			},
			Signature = "test-signature"
		};
}
