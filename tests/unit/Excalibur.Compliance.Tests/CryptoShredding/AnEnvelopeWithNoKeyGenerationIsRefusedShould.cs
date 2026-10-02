// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// The field read path REFUSES an envelope that does not name the generation of key material it was written
/// under, and refuses it before it asks the key store anything and before any associated data is computed.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THE REFUSAL IS LOAD-BEARING AND NOT MERELY TIDY.</b> A subject's key handle is derived from the
/// subject, so it is stable and can be occupied again: destroy a subject's key, let one ordinary write
/// provision another at the same handle, and the version ordinal restarts. Both the handle and the restarted
/// ordinal then answer "not destroyed" truthfully — about material the reader is not holding. The generation
/// is the one identifier a later provisioning cannot change, so it is what the destruction question keys on.
/// </para>
/// <para>
/// <b>AND WHY ITS ABSENCE CANNOT BE TOLERATED.</b> The generation is bound into the associated data only when
/// the caller supplies one, which is what leaves callers with nowhere to carry a generation — the audit
/// envelope among them — working exactly as before. That conditional binding is safe ONLY because this path
/// refuses the absent case: otherwise an envelope with the property stripped would re-read as the
/// no-generation form and authenticate correctly, a downgrade anyone could perform by deleting one JSON
/// field. The refusal makes that inexpressible here while leaving the conditional binding intact there, and
/// the last two arms hold both halves of that statement at once.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AnEnvelopeWithNoKeyGenerationIsRefusedShould
{
	private const string KeyId = "subject-key-handle";
	private const int KeyVersion = 1;
	private const string LiveGeneration = "f3a1c0de5b7948e2a6d4019fcb82e57d";

	private static readonly byte[] Plaintext = "personal data"u8.ToArray();

	/// <summary>
	/// THE LOAD-BEARING ARM. An envelope with no generation is refused by name, and refused EARLY: the key
	/// store is never asked and no decryption is attempted, so no associated data is ever computed from it.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: drop the absent-generation guard and this envelope proceeds to decrypt
	/// under the no-generation associated data, which authenticates — the stripped-field downgrade succeeding
	/// silently. The two <c>MustNotHaveHappened</c> assertions are what make it a refusal rather than a
	/// failure: a guard that ran after the store was asked would satisfy the exception assertion alone.
	/// </remarks>
	[Fact]
	public async Task Refuse_AnEnvelopeCarryingNoKeyGeneration_BeforeAnyKeyStoreQuestionOrDecryption()
	{
		var ledger = LedgerStatingNotDestroyed();
		var (registry, decryptor) = RegistryWhoseProviderWouldSucceed();

		var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, A.Fake<ITenantContext>());

		var refusal = await Should.ThrowAsync<EncryptionException>(
			() => encryptor.DecryptAsync(Envelope(generation: null), CancellationToken.None).AsTask());

		refusal.ErrorCode.ShouldBe(
			EncryptionErrorCode.InvalidCiphertext,
			"an envelope that cannot identify its own key material is refused as unreadable ciphertext, never "
			+ "reported as an erasure and never read");

		A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => decryptor.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// The same refusal for the OTHER shape absence takes. A deleted JSON property deserializes to
	/// <see langword="null"/>, but one present and emptied deserializes to an empty string, and a guard
	/// written against null alone lets that second shape through to the no-generation associated data.
	/// </summary>
	[Fact]
	public async Task Refuse_AnEnvelopeWhoseKeyGenerationIsEmpty_NotOnlyOneThatIsNull()
	{
		var ledger = LedgerStatingNotDestroyed();
		var (registry, decryptor) = RegistryWhoseProviderWouldSucceed();

		var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, A.Fake<ITenantContext>());

		var refusal = await Should.ThrowAsync<EncryptionException>(
			() => encryptor.DecryptAsync(Envelope(generation: string.Empty), CancellationToken.None).AsTask());

		refusal.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
		A.CallTo(() => decryptor.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// An envelope from a layout this build does not write is refused on its declared format version, and
	/// refused even when it happens to carry a generation. The two guards answer different questions, so a
	/// single check standing in for both would pass this arm while leaving the other open.
	/// </summary>
	[Fact]
	public async Task Refuse_AnEnvelopeFromAnEarlierLayout_EvenWhenItCarriesAGeneration()
	{
		var ledger = LedgerStatingNotDestroyed();
		var (registry, decryptor) = RegistryWhoseProviderWouldSucceed();

		var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, A.Fake<ITenantContext>());

		var earlierLayout = Envelope(generation: LiveGeneration) with
		{
			FormatVersion = EncryptedData.CurrentFormatVersion - 1,
		};

		var refusal = await Should.ThrowAsync<EncryptionException>(
			() => encryptor.DecryptAsync(earlierLayout, CancellationToken.None).AsTask());

		refusal.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
		A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => decryptor.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS. Without it every arm above is satisfied by a read path that refuses EVERYTHING, which is the
	/// cheapest way to pass a refusal assertion and would make the whole field-encryption feature inert.
	/// </summary>
	[Fact]
	public async Task StillRead_AnEnvelopeNamingALiveGenerationAtTheCurrentFormatVersion()
	{
		var ledger = LedgerStatingNotDestroyed();
		var (registry, _) = RegistryWhoseProviderWouldSucceed();

		var encryptor = new FieldEncryptor(A.Fake<ISubjectKeyManager>(), registry, ledger, A.Fake<ITenantContext>());

		var plaintext = await encryptor.DecryptAsync(Envelope(generation: LiveGeneration), CancellationToken.None);

		plaintext.ShouldBe(Plaintext);
		A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, LiveGeneration, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// The generation reaches the cipher, not just the envelope: real AES-GCM, real keys, and ciphertext
	/// written under one generation does not open under another.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: omit the generation from the associated data and this decryption
	/// succeeds, which would leave the identifier recorded in the envelope and bound to nothing — a field a
	/// holder of the ciphertext could rewrite at will.
	/// </remarks>
	[Fact]
	public async Task RefuseToOpenCiphertextWrittenUnderOneGenerationUnderAnother()
	{
		using var provider = RealProvider();

		var encrypted = await provider.EncryptAsync(Plaintext, Context(LiveGeneration), CancellationToken.None)
			.ConfigureAwait(false);

		encrypted.KeyGeneration.ShouldBe(
			LiveGeneration,
			"precondition: the write must record the generation it bound, or the read has nothing to check");

		_ = await Should.ThrowAsync<Exception>(
			() => provider.DecryptAsync(
				encrypted, Context("9c2e7b04a1f8436da05e63bc87d91f2a"), CancellationToken.None));
	}

	/// <summary>
	/// The downgrade, at the cipher rather than at the guard. Ciphertext written under a generation does not
	/// open for a reader that omits it, so stripping the property does not silently fall back to the
	/// no-generation form — the field path's refusal above and this tag failure close the same hole from
	/// both sides.
	/// </summary>
	[Fact]
	public async Task RefuseToOpenAGenerationBoundCiphertextForAReaderThatOmitsTheGeneration()
	{
		using var provider = RealProvider();

		var encrypted = await provider.EncryptAsync(Plaintext, Context(LiveGeneration), CancellationToken.None)
			.ConfigureAwait(false);

		_ = await Should.ThrowAsync<Exception>(
			() => provider.DecryptAsync(encrypted, Context(generation: null), CancellationToken.None));
	}

	/// <summary>
	/// LIVENESS, and the separation itself. A caller with nowhere to carry a generation supplies none on
	/// EITHER path and still round-trips, so binding-only-when-present leaves those callers — the audit
	/// envelope among them — untouched by this change.
	/// </summary>
	/// <remarks>
	/// RED on the defect this locks: bind the generation unconditionally, as an empty value rather than as
	/// nothing, and the write and the read would still agree here — so this arm is paired with the one above
	/// it, which fails if the two forms ever became interchangeable.
	/// </remarks>
	[Fact]
	public async Task StillRoundTrip_ACallerThatSuppliesNoGenerationOnEitherPath()
	{
		using var provider = RealProvider();

		var encrypted = await provider.EncryptAsync(Plaintext, Context(generation: null), CancellationToken.None)
			.ConfigureAwait(false);

		var roundTripped = await provider.DecryptAsync(encrypted, Context(generation: null), CancellationToken.None)
			.ConfigureAwait(false);

		roundTripped.ShouldBe(Plaintext);
	}

	private static AesGcmEncryptionProvider RealProvider() =>
		new(
			new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance),
			NullLogger<AesGcmEncryptionProvider>.Instance);

	private static EncryptionContext Context(string? generation) =>
		new() { Purpose = "test", KeyGeneration = generation };

	private static EncryptedData Envelope(string? generation) => new()
	{
		Ciphertext = new byte[32],
		KeyId = KeyId,
		KeyVersion = KeyVersion,
		KeyGeneration = generation,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		Iv = new byte[12],
		AuthTag = new byte[16],
	};

	// A ledger holding no destruction record, so nothing in these arms turns on a destruction statement.
	private static IKeyDestructionLedger LedgerStatingNotDestroyed()
	{
		var ledger = A.Fake<IKeyDestructionLedger>();
		_ = A.CallTo(() => ledger.IsGenerationDestroyedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		return ledger;
	}

	// A registry whose decryption provider would hand back the plaintext. Every refusal arm asserts it was
	// never reached, so a guard that ran too late reddens rather than passing on the exception alone.
	private static (IEncryptionProviderRegistry Registry, IEncryptionProvider Provider)
		RegistryWhoseProviderWouldSucceed()
	{
		var provider = A.Fake<IEncryptionProvider>();
		_ = A.CallTo(() => provider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Plaintext));

		var registry = A.Fake<IEncryptionProviderRegistry>();
		_ = A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(provider);

		return (registry, provider);
	}
}
