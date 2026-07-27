// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;

using FakeItEasy;

using Shouldly;

using Xunit;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Regression lock for the ckb35i fail-closed hardening: <c>SubjectFieldCryptor.EncryptFieldsAsync</c> must
/// REFUSE (throw) when a type declares a data subject (<c>[DataSubjectId]</c>) but resolves ZERO
/// <c>[PersonalData]</c> fields — that mismatch means the classification annotations were lost (e.g.
/// trimmed away), and silently encrypting nothing would persist plaintext personal data (a GDPR breach).
/// Never silent-plaintext.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SubjectFieldCryptorFailClosedShould
{
    [Fact]
    public async Task FailClosed_WhenDataSubjectDeclaredButNoPersonalDataFieldsResolve()
    {
        var cryptor = new SubjectFieldCryptor(A.Fake<IFieldEncryptor>());
        var record = new DataSubjectWithNoPersonalData { SubjectId = "subject-1", Notes = "not classified" };

        // RED on the pre-fix silent no-op (which would persist the record unencrypted); GREEN on the throw.
        await Should.ThrowAsync<EncryptionException>(
            () => cryptor.EncryptFieldsAsync(record, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task NoOp_WhenTypeIsNotADataSubject()
    {
        var cryptor = new SubjectFieldCryptor(A.Fake<IFieldEncryptor>());
        var record = new NotADataSubject { Name = "plain" };

        // No [DataSubjectId] => not a data-subject entity => legitimate no-op, never a false-positive throw.
        await Should.NotThrowAsync(
            () => cryptor.EncryptFieldsAsync(record, CancellationToken.None).AsTask());
    }

    // Declares a data subject but carries NO [PersonalData] field — the trimmed/registry-mismatch case.
    private sealed class DataSubjectWithNoPersonalData
    {
        [DataSubjectId]
        public string SubjectId { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }

    // Not a data-subject entity at all.
    private sealed class NotADataSubject
    {
        public string Name { get; set; } = string.Empty;
    }
}
