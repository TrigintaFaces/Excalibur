// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch.Security.Encryption;

/// <summary>
/// bd-nkmir4 (S840, AC-8) — independent regression lock (author≠impl, TestsDeveloper).
/// <para>
/// ElasticSearch field encryption must survive key rotation: a field encrypted under key version N must
/// still decrypt after rotation to N+1, because decryption resolves the key BY the version stamped on the
/// ciphertext — not the current key. The pre-fix code hardcoded the version and resolved the CURRENT key
/// (and the provider overwrote prior versions on rotation), so every pre-rotation PII/PHI field became
/// permanently undecryptable — silent, irreversible data loss.
/// </para>
/// <para>
/// Also locks ADR-336 Amendment 2 (decrypt rejects an unknown envelope <c>FormatVersion</c>) and the folded
/// bd-gucy1d (the auto-rotation timer must arm safely with the default 90-day interval, which exceeds
/// <see cref="System.Threading.Timer"/>'s max dueTime ~49.7d). All RED on the pre-fix code.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RotationSafeFieldEncryptionShould
{
    [Fact]
    public async Task DecryptFieldEncryptedUnderAPriorKeyVersionAfterRotation()
    {
        // Arrange — encrypt a field under the Confidential key (version N).
        using var sut = CreateEncryptor();
        const string original = "123-45-6789";
        var encrypted = await sut.EncryptFieldAsync(
            "ssn", original, ElasticSearchDataClassification.Confidential, CancellationToken.None);

        // Act — rotate the EXACT key this field used (Confidential), then decrypt the pre-rotation field.
        var rotation = await sut.RotateEncryptionKeysAsync(
            ElasticSearchDataClassification.Confidential, CancellationToken.None);
        var decrypted = await sut.DecryptFieldAsync("ssn", encrypted, CancellationToken.None);

        // Assert — rotation actually advanced the version (non-vacuity), and the version-N field still
        // round-trips via its STAMPED version. RED on pre-fix (decrypt resolved the current key → the
        // rotated key cannot authenticate pre-rotation ciphertext → SecurityException).
        rotation.Success.ShouldBeTrue();
        rotation.NewKeyVersion.ShouldNotBe(rotation.PreviousKeyVersion);
        decrypted.ShouldNotBeNull();
        decrypted.ToString().ShouldBe(original);
    }

    [Fact]
    public async Task RejectUnknownEnvelopeFormatVersionOnDecrypt()
    {
        // Arrange — a real encrypted field, copied with an unknown envelope format version (Amendment 2).
        using var sut = CreateEncryptor();
        var encrypted = await sut.EncryptFieldAsync(
            "ssn", "123-45-6789", ElasticSearchDataClassification.Confidential, CancellationToken.None);
        var unknownFormat = new EncryptedFieldResult(
            encrypted.EncryptedValue,
            encrypted.Algorithm,
            encrypted.KeyVersion,
            encrypted.InitializationVector,
            encrypted.AuthenticationTag,
            encrypted.Classification,
            formatVersion: "999");

        // Act & Assert — decrypt MUST surface an unknown-format error, never a best-effort parse.
        _ = await Should.ThrowAsync<SecurityException>(
            () => sut.DecryptFieldAsync("ssn", unknownFormat, CancellationToken.None));
    }

    [Fact]
    public void ArmAutoRotationTimerSafelyWithDefaultNinetyDayInterval()
    {
        // bd-gucy1d (folded into nkmir4): with key rotation supported and a 90-day interval — which
        // exceeds System.Threading.Timer's max dueTime (~49.7d) — constructing the encryptor MUST NOT
        // throw ArgumentOutOfRangeException. RED on pre-fix (unclamped Timer overflow on enable).
        Should.NotThrow(() =>
        {
            using var encryptor = new FieldEncryptor(
                new LocalKeyProvider(),
                Options.Create(new EncryptionOptions
                {
                    KeyManagement = new KeyManagementOptions { KeyRotationInterval = TimeSpan.FromDays(90) },
                }),
                NullLogger<FieldEncryptor>.Instance);
        });
    }

    private static FieldEncryptor CreateEncryptor() =>
        new(
            new LocalKeyProvider(),
            Options.Create(new EncryptionOptions
            {
                // Disable the scheduled rotation timer; these tests drive rotation manually.
                KeyManagement = new KeyManagementOptions { KeyRotationInterval = TimeSpan.Zero },
            }),
            NullLogger<FieldEncryptor>.Instance);
}
