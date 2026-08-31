// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Excalibur.Compliance;
using Excalibur.Compliance.Aws;

using Excalibur.Dispatch.Integration.Tests.Compliance.Fixtures;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.KeyManagement;

/// <summary>
/// Binds the one property no metadata assertion can establish: that data encrypted before a rotation can
/// still be decrypted after it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this exists for.</b> Rotation used to call <c>DisableKey</c> on the superseded key, under
/// a comment saying it was kept "for decryption". AWS documents a disabled key as unusable for
/// <em>every</em> cryptographic operation, decryption included, so every rotation silently made all prior
/// ciphertext unreadable until an operator manually re-enabled the key. Routine key rotation destroyed
/// access to data.
/// </para>
/// <para>
/// <b>Why the conformance kit could not catch it.</b> Every arm there inspects key metadata. Metadata
/// after the defective rotation looked entirely correct — a new key existed, the old one was reported as
/// superseded — because the damage is only observable by attempting a cryptographic operation. So this arm
/// encrypts, rotates, and then decrypts. That round trip is the assertion; anything short of it would
/// re-create the blind spot.
/// </para>
/// <para>
/// <b>Both halves.</b> The prior key must still decrypt (the property that was broken), and the rotation
/// must genuinely have produced a distinct new key (otherwise "decryption still works" is satisfied by a
/// rotation that did nothing at all, which would pass while the feature was inert).
/// </para>
/// <para>
/// Runs against real LocalStack KMS and is never skip-gated: a real-infrastructure arm that passes by
/// being skipped is exactly the gap that let this ship.
/// </para>
/// </remarks>
[Collection(LocalStackTestCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
public sealed class AwsKmsRotationKeepsPriorCiphertextReadableShould : IDisposable
{
    private readonly LocalStackContainerFixture _fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public AwsKmsRotationKeepsPriorCiphertextReadableShould(LocalStackContainerFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task DecryptCiphertextWrittenBeforeTheRotation()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "This arm proves rotation does not destroy access to existing data. Passing it by being "
            + "skipped would restore the exact blind spot that let a data-loss defect ship. "
            + (_fixture.InitializationError ?? "LocalStack container required."));

        var kms = _fixture.CreateKmsClient();
        var options = new AwsKmsOptions
        {
            KeyAliasPrefix = $"rotation-{Guid.NewGuid():N}",
            KeyPolicy = new AwsKmsKeyPolicyOptions { EnableAutoRotation = false },
        };

        var provider = new AwsKmsProvider(
            kms,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<AwsKmsProvider>.Instance,
            _cache);

        try
        {
            var keyId = $"rotation-test-{Guid.NewGuid():N}";

            var created = await provider.RotateKeyAsync(
                keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

            created.NewKey.ShouldNotBeNull("the key must exist before anything is encrypted under it.");

            // Encrypt under version 1, addressing it exactly as a consumer would — through the alias.
            var plaintext = Encoding.UTF8.GetBytes("data written before the rotation");
            var encrypted = await kms.EncryptAsync(
                new EncryptRequest
                {
                    KeyId = options.BuildKeyAlias(keyId),
                    Plaintext = new MemoryStream(plaintext),
                },
                CancellationToken.None).ConfigureAwait(false);

            var rotated = await provider.RotateKeyAsync(
                keyId, EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None).ConfigureAwait(false);

            // Liveness: the rotation actually happened. Without this, an inert rotation would satisfy the
            // decrypt assertion below and the arm would pass while proving nothing.
            rotated.NewKey.ShouldNotBeNull("rotation must produce a new key.");
            rotated.NewKey!.Version.ShouldBeGreaterThan(
                created.NewKey!.Version,
                "the rotated key must be a later version, or nothing was rotated and this arm proves nothing.");

            // THE ASSERTION. Ciphertext written before the rotation must still be readable after it.
            var decrypted = await kms.DecryptAsync(
                new DecryptRequest { CiphertextBlob = encrypted.CiphertextBlob },
                CancellationToken.None).ConfigureAwait(false);

            Encoding.UTF8.GetString(decrypted.Plaintext.ToArray()).ShouldBe(
                "data written before the rotation",
                "data encrypted before a rotation must remain decryptable after it. Disabling the "
                + "superseded key makes it undecryptable, which is data loss on the routine path.");
        }
        finally
        {
            provider.Dispose();
        }
    }

    public void Dispose() => _cache.Dispose();
}
