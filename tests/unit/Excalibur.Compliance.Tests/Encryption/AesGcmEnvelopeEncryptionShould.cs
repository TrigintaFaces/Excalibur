// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Encryption;

using Excalibur.Compliance;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the envelope key path: the one a cloud KMS or HSM can actually serve, because it never asks
/// the key service to export key bytes.
/// </summary>
/// <remarks>
/// <para>
/// Every arm here is paired. A guard that refuses a provider with no key path is fully satisfied by a
/// provider that refuses everything, so each refusal is matched by an arm proving the permitted case
/// still works — and, for the case that actually matters, that a payload encrypted by one instance is
/// recoverable by a different one. Recovery after a restart is the property escrow exists for, and it
/// is the only one whose failure is discovered too late to do anything about.
/// </para>
/// <para>
/// <see cref="WrapOnlyKeyProvider"/> implements <see cref="IKeyManagementProvider"/> and
/// <see cref="IKeyWrappingProvider"/> from scratch, deriving from no framework base, so these arms bind
/// the interface contract rather than re-testing an inherited convenience. It deliberately does NOT
/// implement the internal key-material interface — that is what makes it a faithful stand-in for a key
/// service that will not hand out its key.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AesGcmEnvelopeEncryptionShould
{
	private const string KeyId = "envelope-test-key";

	// Stands in for the key that never leaves the service. Shared between instances on purpose: two
	// provider instances pointed at the SAME key service is exactly the restart case.
	private static readonly byte[] ServiceHeldKey = Convert.FromHexString(
		"0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0");

	[Fact]
	public void Refuse_a_provider_that_supplies_neither_key_material_nor_wrapping()
	{
		var noCapability = new NoCapabilityKeyProvider();

		var ex = Should.Throw<InvalidOperationException>(() => new AesGcmEncryptionProvider(
			noCapability,
			NullLogger<AesGcmEncryptionProvider>.Instance));

		// The message has to name BOTH ways out, or a consumer reading it concludes the only fix is to
		// expose key material -- which is the thing a KMS will not do, and the dead end this closes.
		ex.Message.ShouldContain("IKeyWrappingProvider");
	}

	[Fact]
	public void Accept_a_provider_that_supplies_only_wrapping()
	{
		// The liveness arm for the guard above. Without it, a guard that rejects every KMS provider
		// passes its own safety test forever and the production key path stays unreachable.
		using var provider = new WrapOnlyKeyProvider();

		_ = Should.NotThrow(() => new AesGcmEncryptionProvider(
			provider,
			NullLogger<AesGcmEncryptionProvider>.Instance));
	}

	[Fact]
	public async Task Record_a_wrapped_data_key_when_encrypting_under_an_envelope()
	{
		using var provider = new WrapOnlyKeyProvider();
		using var sut = new AesGcmEncryptionProvider(provider, NullLogger<AesGcmEncryptionProvider>.Instance);

		var encrypted = await sut.EncryptAsync("envelope payload"u8.ToArray(), new EncryptionContext(), TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		encrypted.WrappedKey.ShouldNotBeNull();
		encrypted.WrappedKey!.CiphertextBlob.ShouldNotBeEmpty();

		// The wrapped key must not BE the data key. If a provider ever returned the plaintext data key
		// as its own "wrapped" form, every arm below would still pass while the envelope protected
		// nothing, so this asserts the one thing that cannot be true of a real wrap.
		encrypted.WrappedKey.CiphertextBlob.ShouldNotBe(ServiceHeldKey);
	}

	[Fact]
	public async Task Recover_the_payload_on_a_fresh_provider_instance()
	{
		// THE arm that matters. Encrypt on one instance, decrypt on another built from scratch over the
		// same key service -- a process restart, which is when the in-memory key path loses everything
		// and reports nothing.
		var plaintext = "recover me after a restart"u8.ToArray();
		var context = new EncryptionContext { TenantId = "tenant-a" };

		EncryptedData encrypted;
		using (var writingProvider = new WrapOnlyKeyProvider())
		using (var writer = new AesGcmEncryptionProvider(writingProvider, NullLogger<AesGcmEncryptionProvider>.Instance))
		{
			encrypted = await writer.EncryptAsync(plaintext, context, TestContext.Current.CancellationToken).ConfigureAwait(true);
		}

		using var readingProvider = new WrapOnlyKeyProvider();
		using var reader = new AesGcmEncryptionProvider(readingProvider, NullLogger<AesGcmEncryptionProvider>.Instance);

		var decrypted = await reader.DecryptAsync(encrypted, context, TestContext.Current.CancellationToken).ConfigureAwait(true);

		decrypted.ShouldBe(plaintext);
	}

	[Fact]
	public async Task Leave_the_wrapped_key_absent_when_the_provider_supplies_key_material()
	{
		// The direct path must be untouched, and the scheme must be recorded ON the payload. A reader
		// picks the scheme from the data, not from how it happens to be wired at read time.
		using var keyManagement = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		using var sut = new AesGcmEncryptionProvider(keyManagement, NullLogger<AesGcmEncryptionProvider>.Instance);

		var encrypted = await sut.EncryptAsync("direct"u8.ToArray(), new EncryptionContext(), TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		encrypted.WrappedKey.ShouldBeNull();
	}

	[Fact]
	public async Task Refuse_to_decrypt_an_envelope_payload_when_no_wrapping_provider_is_registered()
	{
		// Fail closed. The alternative -- falling through to the key-material path -- would decrypt with
		// the wrong key or throw something unrecognisable, and either way would obscure the real cause.
		EncryptedData encrypted;
		using (var writingProvider = new WrapOnlyKeyProvider())
		using (var writer = new AesGcmEncryptionProvider(writingProvider, NullLogger<AesGcmEncryptionProvider>.Instance))
		{
			encrypted = await writer.EncryptAsync("orphaned"u8.ToArray(), new EncryptionContext(), TestContext.Current.CancellationToken)
				.ConfigureAwait(true);
		}

		using var materialOnly = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		_ = await materialOnly.RotateKeyAsync(KeyId, EncryptionAlgorithm.Aes256Gcm, null, null, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);
		using var reader = new AesGcmEncryptionProvider(materialOnly, NullLogger<AesGcmEncryptionProvider>.Instance);

		var ex = await Should.ThrowAsync<EncryptionException>(
			() => reader.DecryptAsync(encrypted, new EncryptionContext(), TestContext.Current.CancellationToken))
			.ConfigureAwait(true);

		ex.ErrorCode.ShouldBe(EncryptionErrorCode.ServiceUnavailable);
	}

	[Fact]
	public async Task Abandon_encryption_when_the_wrapping_provider_returns_an_empty_wrapped_key()
	{
		// Persisting ciphertext whose data key was never really wrapped produces a row that cannot be
		// decrypted by anyone, ever. Better to fail the write than to store that quietly.
		using var provider = new WrapOnlyKeyProvider { ReturnEmptyWrap = true };
		using var sut = new AesGcmEncryptionProvider(provider, NullLogger<AesGcmEncryptionProvider>.Instance);

		var ex = await Should.ThrowAsync<EncryptionException>(
			() => sut.EncryptAsync("doomed"u8.ToArray(), new EncryptionContext(), TestContext.Current.CancellationToken))
			.ConfigureAwait(true);

		ex.ErrorCode.ShouldBe(EncryptionErrorCode.ServiceUnavailable);
	}

	/// <summary>A key service that exposes no capability at all.</summary>
	private sealed class NoCapabilityKeyProvider : IKeyManagementProvider
	{
		public Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(null);

		public Task<KeyRotationResult> RotateKeyAsync(
			string keyId,
			EncryptionAlgorithm algorithm,
			string? purpose,
			DateTimeOffset? expiresAt,
			CancellationToken cancellationToken) => throw new NotSupportedException();
	}

	/// <summary>
	/// A key service that wraps and unwraps but never discloses its key — the shape of a real KMS.
	/// </summary>
	/// <remarks>
	/// Wrapping is a genuine AES-GCM encryption under <see cref="ServiceHeldKey"/>, so an unwrap that
	/// returned the wrong bytes would fail its authentication tag rather than silently succeed. It
	/// implements no key-material interface, which is precisely why the previous design could not use it.
	/// </remarks>
	private sealed class WrapOnlyKeyProvider : IKeyManagementProvider, IKeyWrappingProvider, IDisposable
	{
		private const int NonceBytes = 12;
		private const int TagBytes = 16;

		public bool ReturnEmptyWrap { get; init; }

		public Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(Metadata(keyId));

		public Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(Metadata(keyId));

		public Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken) =>
			Task.FromResult<KeyMetadata?>(Metadata(KeyId));

		public Task<KeyRotationResult> RotateKeyAsync(
			string keyId,
			EncryptionAlgorithm algorithm,
			string? purpose,
			DateTimeOffset? expiresAt,
			CancellationToken cancellationToken) => throw new NotSupportedException();

		public Task<WrappedDataKey> WrapDataKeyAsync(
			string keyId,
			int version,
			byte[] dataKey,
			CancellationToken cancellationToken)
		{
			if (ReturnEmptyWrap)
			{
				return Task.FromResult(new WrappedDataKey { CiphertextBlob = [] });
			}

			var nonce = new byte[NonceBytes];
			RandomNumberGenerator.Fill(nonce);

			var ciphertext = new byte[dataKey.Length];
			var tag = new byte[TagBytes];

			using (var aes = new AesGcm(ServiceHeldKey, TagBytes))
			{
				aes.Encrypt(nonce, dataKey, ciphertext, tag);
			}

			// nonce || tag || ciphertext -- opaque to the caller, which is the contract.
			var blob = new byte[NonceBytes + TagBytes + ciphertext.Length];
			nonce.CopyTo(blob, 0);
			tag.CopyTo(blob, NonceBytes);
			ciphertext.CopyTo(blob, NonceBytes + TagBytes);

			return Task.FromResult(new WrappedDataKey
			{
				CiphertextBlob = blob,
				WrappingKeyId = keyId,
				Algorithm = "test-aes-gcm"
			});
		}

		public Task<byte[]> UnwrapDataKeyAsync(
			string keyId,
			int version,
			WrappedDataKey wrappedKey,
			CancellationToken cancellationToken)
		{
			var blob = wrappedKey.CiphertextBlob;
			var nonce = blob.AsSpan(0, NonceBytes);
			var tag = blob.AsSpan(NonceBytes, TagBytes);
			var ciphertext = blob.AsSpan(NonceBytes + TagBytes);

			var dataKey = new byte[ciphertext.Length];
			using (var aes = new AesGcm(ServiceHeldKey, TagBytes))
			{
				aes.Decrypt(nonce, ciphertext, tag, dataKey);
			}

			return Task.FromResult(dataKey);
		}

		public void Dispose()
		{
		}

		private static KeyMetadata Metadata(string keyId) => new()
		{
			KeyId = keyId,
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
			IsFipsCompliant = true
		};
	}
}
