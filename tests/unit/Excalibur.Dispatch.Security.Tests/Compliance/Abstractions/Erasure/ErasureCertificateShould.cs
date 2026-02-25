// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureCertificateShould
{
    [Fact]
    public void CreateWithRequiredProperties()
    {
        var cert = new ErasureCertificate
        {
            CertificateId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            DataSubjectReference = "hash-of-user",
            RequestReceivedAt = DateTimeOffset.UtcNow.AddDays(-1),
            CompletedAt = DateTimeOffset.UtcNow,
            Method = ErasureMethod.CryptographicErasure,
            Summary = new ErasureSummary { KeysDeleted = 3, RecordsAffected = 15 },
            Verification = new VerificationSummary
            {
                Verified = true,
                Methods = VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem,
                VerifiedAt = DateTimeOffset.UtcNow
            },
            LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
            Signature = "sig-abc",
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
        };

        cert.Method.ShouldBe(ErasureMethod.CryptographicErasure);
        cert.Summary.KeysDeleted.ShouldBe(3);
        cert.Verification.Verified.ShouldBeTrue();
        cert.Exceptions.ShouldBeEmpty();
        cert.Version.ShouldBe("1.0");
    }

    [Fact]
    public void SupportErasureExceptions()
    {
        var cert = new ErasureCertificate
        {
            CertificateId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            DataSubjectReference = "hash",
            RequestReceivedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = ErasureMethod.Hybrid,
            Summary = new ErasureSummary(),
            Verification = new VerificationSummary
            {
                Verified = true,
                Methods = VerificationMethod.AuditLog,
                VerifiedAt = DateTimeOffset.UtcNow
            },
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            Signature = "sig",
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7),
            Exceptions = new[]
            {
                new ErasureException
                {
                    Basis = LegalHoldBasis.LegalObligation,
                    DataCategory = "financial",
                    Reason = "Tax retention",
                    RetentionPeriod = TimeSpan.FromDays(365 * 7)
                }
            }
        };

        cert.Exceptions.Count.ShouldBe(1);
        cert.Exceptions[0].Basis.ShouldBe(LegalHoldBasis.LegalObligation);
    }

    [Fact]
    public void CreateErasureSummaryWithDefaults()
    {
        var summary = new ErasureSummary();

        summary.KeysDeleted.ShouldBe(0);
        summary.RecordsAffected.ShouldBe(0);
        summary.DataCategories.ShouldBeEmpty();
        summary.TablesAffected.ShouldBeEmpty();
        summary.DataSizeBytes.ShouldBe(0);
    }

    [Fact]
    public void CreateVerificationSummaryWithDefaults()
    {
        var summary = new VerificationSummary
        {
            Verified = false,
            Methods = VerificationMethod.None,
            VerifiedAt = DateTimeOffset.UtcNow
        };

        summary.ReportHash.ShouldBeNull();
        summary.DeletedKeyIds.ShouldBeEmpty();
        summary.Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(VerificationMethod.None, 0)]
    [InlineData(VerificationMethod.AuditLog, 1)]
    [InlineData(VerificationMethod.DecryptionFailure, 8)]
    public void HaveCorrectFlagValues(VerificationMethod method, int expectedValue)
    {
        ((int)method).ShouldBe(expectedValue);
    }

    [Fact]
    public void SupportCombinedVerificationMethods()
    {
        var combined = VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem;
        combined.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
        combined.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeTrue();
        combined.HasFlag(VerificationMethod.HsmAttestation).ShouldBeFalse();
    }
}
