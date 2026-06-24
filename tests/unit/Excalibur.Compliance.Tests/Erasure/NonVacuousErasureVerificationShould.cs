// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly IKeyManagementProvider _keyProvider = A.Fake<IKeyManagementProvider>();
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
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Returns((KeyMetadata?)null);

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

        A.CallTo(() => _keyProvider.GetKeyAsync("key-1", A<CancellationToken>._))
            .Returns((KeyMetadata?)null); // key confirmed gone

        var sut = CreateService(new ErasureOptions { VerificationMethods = VerificationMethod.KeyManagementSystem });

        // Act
        var result = await sut.VerifyErasureAsync(requestId, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Verified.ShouldBeTrue();
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

    private static ErasureCertificate CreateCertificateWithoutDeletedKeyIds(Guid requestId, Guid certificateId) =>
        CreateCertificate(requestId, certificateId, deletedKeyIds: []);

    private static ErasureCertificate CreateCertificateWithDeletedKeyIds(Guid requestId, Guid certificateId, params string[] keyIds) =>
        CreateCertificate(requestId, certificateId, deletedKeyIds: keyIds);

    private static ErasureCertificate CreateCertificate(Guid requestId, Guid certificateId, string[] deletedKeyIds) =>
        new()
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
            Signature = "test-signature",
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
        };
}
