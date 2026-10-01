// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the re-encryption decision to an EQUALITY test on the key version, never to an ordering one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question being asked.</b> "Is this ciphertext under the key version that is active now?" That is
/// equality. Answering it with <c>&lt;</c> requires the version ordinal to be ORDERED, which is a promise no
/// provider is asked for and which one provider family cannot keep: where a backend's key versions are
/// opaque identifiers, the ordinal is synthesised from a hash of one, so <c>&lt;</c> answers at random and
/// roughly half of stale ciphertext reports as current -- readable only under a generation the consumer
/// believes they retired, with nothing reporting it.
/// </para>
/// <para>
/// <b>Why comparing creation instants is worse rather than better, recorded so nobody re-proposes it.</b>
/// One provider reads every version's creation time from the FIRST version, so all versions of a handle
/// report the same instant and an ordering comparison is always false -- nothing is ever re-encrypted, on a
/// provider that was otherwise correct. Two providers substitute the local clock when the backend supplies
/// no instant, which can sort an old version newest and invert the answer in the unsafe direction.
/// </para>
/// <para>
/// <b>What equality needs, and it is the one promise every provider must keep.</b> That a key identifier and
/// version together designate one key version -- which has to hold for ciphertext to be decryptable at all.
/// So it is correct on synthesised ordinals, on real ordinals, and on whatever a future provider reports,
/// with no clock and no second lookup.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class StaleCiphertextIsDetectedByVersionEqualityNotOrderingShould : IDisposable
{
	private readonly IEncryptionProvider _inner = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagement = A.Fake<IKeyManagementProvider>();
	private readonly RotatingEncryptionProvider _sut;

	public StaleCiphertextIsDetectedByVersionEqualityNotOrderingShould() =>
		_sut = new RotatingEncryptionProvider(
			_inner,
			_keyManagement,
			NullLogger<RotatingEncryptionProvider>.Instance);

	/// <summary>
	/// THE ARM. The active version's ordinal is LOWER than the ciphertext's, which is a state a synthesised
	/// ordinal produces freely. The ciphertext is not under the active version, so it is stale.
	/// </summary>
	/// <remarks>
	/// RED under an ordering comparison: 42 &lt; 7 is false, so the stale value is reported current and
	/// returned untouched. No arm built against an ordinal provider can reach this, because there the ordinal
	/// really is ordered and the two answers agree on every input -- which is why this survived every suite.
	/// </remarks>
	[Fact]
	public async Task ReEncrypt_WhenTheActiveVersionsOrdinalIsLowerThanTheCiphertexts()
	{
		var stale = Envelope(keyVersion: 42);
		ActiveKeyIs(version: 7);

		var reEncrypted = Envelope(keyVersion: 7);
		A.CallTo(() => _inner.DecryptAsync(stale, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]>([9, 9, 9]));
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(reEncrypted));

		var result = await _sut
			.ReEncryptAsync(stale, new EncryptionContext { KeyId = "k1" }, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		result.ShouldBeSameAs(
			reEncrypted,
			"this ciphertext is not under the active version, so it is stale whatever the ordinals' relative "
			+ "size suggests. Returning it untouched leaves consumer data readable only under a generation "
			+ "the consumer believes they retired, and nothing reports it.");
	}

	/// <summary>
	/// LIVENESS. A value already under the active version must NOT be re-encrypted, so the arm above cannot be
	/// satisfied by a method that always re-encrypts.
	/// </summary>
	[Fact]
	public async Task NotReEncrypt_WhenTheValueIsAlreadyUnderTheActiveVersion()
	{
		var current = Envelope(keyVersion: 7);
		ActiveKeyIs(version: 7);

		var result = await _sut
			.ReEncryptAsync(current, new EncryptionContext { KeyId = "k1" }, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		result.ShouldBeSameAs(current, "a value already under the active version is owed no work");

		A.CallTo(() => _inner.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// The decision resolves nothing. A second provider call to look up the encrypting version would inherit
	/// that lookup's failure modes -- on a backend with a synthesised ordinal it can resolve the wrong
	/// version -- so the equality test must read the envelope's own stored value and the active key's, and
	/// nothing else.
	/// </summary>
	[Fact]
	public async Task NotResolveTheEncryptingVersion_BecauseThatLookupIsNotAlwaysInjective()
	{
		ActiveKeyIs(version: 7);

		var reEncrypted = Envelope(keyVersion: 7);
		A.CallTo(() => _inner.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]>([9, 9, 9]));
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(reEncrypted));

		_ = await _sut
			.ReEncryptAsync(Envelope(keyVersion: 42), new EncryptionContext { KeyId = "k1" }, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		A.CallTo(() => _keyManagement.GetKeyVersionAsync(A<string>._, A<int>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// A different handle is stale regardless of the version ordinal, so the handle term stays independent of
	/// the version term rather than being subsumed by it.
	/// </summary>
	[Fact]
	public async Task ReEncrypt_WhenTheHandleDiffersButTheVersionOrdinalMatches()
	{
		var otherHandle = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			Iv = new byte[12],
			KeyId = "k-old",
			KeyVersion = 7,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
		};

		ActiveKeyIs(version: 7);

		var reEncrypted = Envelope(keyVersion: 7);
		A.CallTo(() => _inner.DecryptAsync(otherHandle, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]>([9, 9, 9]));
		A.CallTo(() => _inner.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(reEncrypted));

		var result = await _sut
			.ReEncryptAsync(otherHandle, new EncryptionContext { KeyId = "k-old" }, TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		result.ShouldBeSameAs(
			reEncrypted,
			"equal version ordinals across DIFFERENT handles are not the same key version, so dropping the "
			+ "handle term would report this stale value as current");
	}

	public void Dispose() => _sut.Dispose();

	private static EncryptedData Envelope(int keyVersion) => new()
	{
		Ciphertext = [1, 2, 3],
		Iv = new byte[12],
		KeyId = "k1",
		KeyVersion = keyVersion,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
	};

	private void ActiveKeyIs(int version) =>
		A.CallTo(() => _keyManagement.GetActiveKeyAsync(null, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "k1",
				Version = version,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
			}));
}
