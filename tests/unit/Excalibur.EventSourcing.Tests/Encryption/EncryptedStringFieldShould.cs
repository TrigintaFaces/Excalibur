// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Locks that <c>[EncryptedField]</c> on a <see cref="string"/> property is honoured rather than skipped.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these arms FAILS against the previous behaviour, where all three encryption paths filtered
/// <c>PropertyType == typeof(byte[])</c>: an annotated string was dropped from selection, so it was written
/// to the store in plaintext with no error and no warning.
/// </para>
/// <para>
/// SAFETY (the plaintext is absent from the stored value) is paired with LIVENESS (the round-trip returns
/// the original string) and a NON-VACUITY control (<see cref="EncryptionMode.Disabled"/> stores the
/// plaintext, so the safety assertion is provably capable of failing).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptedStringFieldShould
{
	private const string Plaintext = "SUPER-SECRET-API-KEY-8c21f7";

	private readonly IProjectionStore<StringSecretProjection> _innerStore = A.Fake<IProjectionStore<StringSecretProjection>>();
	private readonly IEncryptionProviderRegistry _registry = A.Fake<IEncryptionProviderRegistry>();
	private readonly IEncryptionProvider _provider = A.Fake<IEncryptionProvider>();
	private readonly CancellationToken _ct = CancellationToken.None;

	private static EncryptedData Envelope(byte[] ciphertext) => new()
	{
		Ciphertext = ciphertext,
		KeyId = "key-1",
		KeyVersion = 1,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		Iv = [11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22],
	};

	private EncryptingProjectionStoreDecorator<StringSecretProjection> CreateDecorator(
		EncryptionMode mode = EncryptionMode.EncryptAndDecrypt) =>
		new(_innerStore, _registry, Options.Create(new EncryptionOptions
		{
			Mode = mode,
			DefaultPurpose = "test",
		}), global::Excalibur.Dispatch.UntenantedContext.Instance);

	[Fact]
	public async Task EncryptAnAnnotatedStringSoThePlaintextIsNotStored()
	{
		var decorator = CreateDecorator();
		var projection = new StringSecretProjection { Id = "proj-1", ApiKey = Plaintext };

		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(Envelope([99, 98, 97])));

		await decorator.UpsertAsync("proj-1", projection, _ct);

		// The property no longer holds the plaintext anywhere in its text.
		projection.ApiKey.ShouldNotBeNull();
		projection.ApiKey.ShouldNotContain(Plaintext);

		// A string property carries its envelope Base64-encoded, and the decoded bytes carry the "EXCR" magic.
		projection.ApiKey.ShouldStartWith(EncryptedFieldBinding.StringEnvelopePrefix);
		var decoded = Convert.FromBase64String(
			projection.ApiKey[EncryptedFieldBinding.StringEnvelopePrefix.Length..]);
		EncryptedData.IsFieldEncrypted(decoded).ShouldBeTrue();
	}

	[Fact]
	public async Task EncryptTheStringsUtf8BytesRatherThanSomeOtherEncoding()
	{
		var decorator = CreateDecorator();
		var projection = new StringSecretProjection { Id = "proj-1", ApiKey = Plaintext };
		byte[]? handedToProvider = null;

		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, _ct))
			.Invokes((byte[] p, EncryptionContext _, CancellationToken _) => handedToProvider = p)
			.Returns(Task.FromResult(Envelope([1, 2, 3])));

		await decorator.UpsertAsync("proj-1", projection, _ct);

		handedToProvider.ShouldNotBeNull();
		handedToProvider.ShouldBe(Encoding.UTF8.GetBytes(Plaintext));
	}

	[Fact]
	public async Task NotDoubleEncryptAStringThatAlreadyHoldsAnEnvelope()
	{
		var decorator = CreateDecorator();
		// An envelope already written by a prior encrypt: magic bytes, Base64-encoded for a string property.
		var alreadyEncrypted = EncryptedFieldBinding.StringEnvelopePrefix
			+ Convert.ToBase64String([0x45, 0x58, 0x43, 0x52, 1, 2, 3, 4, 5]);
		var projection = new StringSecretProjection { Id = "proj-1", ApiKey = alreadyEncrypted };

		await decorator.UpsertAsync("proj-1", projection, _ct);

		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
		projection.ApiKey.ShouldBe(alreadyEncrypted);
	}

	[Fact]
	public async Task EncryptAPlaintextWhoseBase64DecodesToTheEnvelopeMagic()
	{
		// REGRESSION, and the reason the marker exists. "RVhDUmVwb3J0" is ordinary plaintext, but it
		// Base64-decodes to the bytes "EXCReport" - which begin with EncryptedData.MagicBytes. A
		// decode-first "is it already encrypted?" test answers YES here, skips the field, and stores the
		// plaintext in the clear: the exact defect this bead fixes, reintroduced. The marker test cannot
		// be fooled this way, because no Base64 output contains the prefix separator.
		const string PlaintextThatDecodesToMagic = "RVhDUmVwb3J0";
		Convert.FromBase64String(PlaintextThatDecodesToMagic)[..4]
			.ShouldBe(new byte[] { 0x45, 0x58, 0x43, 0x52 });

		var decorator = CreateDecorator();
		var projection = new StringSecretProjection { Id = "proj-1", ApiKey = PlaintextThatDecodesToMagic };

		A.CallTo(() => _registry.GetPrimary()).Returns(_provider);
		A.CallTo(() => _provider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, _ct))
			.Returns(Task.FromResult(Envelope([7, 7, 7])));

		await decorator.UpsertAsync("proj-1", projection, _ct);

		projection.ApiKey.ShouldNotBe(PlaintextThatDecodesToMagic);
		projection.ApiKey!.ShouldStartWith(EncryptedFieldBinding.StringEnvelopePrefix);
	}

	[Fact]
	public async Task RefuseToTreatACorruptEnvelopeAsPlaintext()
	{
		// A truncated ciphertext (a column too narrow) still carries the marker but no longer decodes.
		// Reporting "not an envelope" would hand the caller ciphertext as though it were decrypted, so
		// this throws instead: the malformed case must leave the value set entirely.
		var decorator = CreateDecorator();
		var projection = new StringSecretProjection
		{
			Id = "proj-1",
			ApiKey = EncryptedFieldBinding.StringEnvelopePrefix + "!!!not-base64!!!",
		};

		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct))
			.Returns(Task.FromResult<StringSecretProjection?>(projection));

		// The READ path refuses. (The WRITE path deliberately does not - see
		// StillStoreARecordWhoseExistingStoredValueIsCorrupt, which is this same value on that path.)
		_ = await Should.ThrowAsync<EncryptionException>(
			() => decorator.GetByIdAsync("proj-1", _ct));
	}

	[Fact]
	public void StoreThePlaintextWhenEncryptionIsDisabled_NonVacuityControl()
	{
		// The control for the safety arm: with encryption off the plaintext IS stored, so the assertion
		// in EncryptAnAnnotatedStringSoThePlaintextIsNotStored is capable of failing and is not vacuous.
		var decorator = CreateDecorator(EncryptionMode.Disabled);
		var projection = new StringSecretProjection { Id = "proj-1", ApiKey = Plaintext };

		_ = decorator.UpsertAsync("proj-1", projection, _ct);

		projection.ApiKey.ShouldBe(Plaintext);
	}

	[Fact]
	public void RejectAnAnnotationOnATypeThatCannotBeEncrypted()
	{
		// Fail fast rather than skip: the annotation never encrypted anything, so refusing it withdraws
		// no working behaviour — it discloses protection the consumer did not have.
		var ex = Should.Throw<EncryptionException>(() =>
			new EncryptingProjectionStoreDecorator<UnencryptableProjection>(
				A.Fake<IProjectionStore<UnencryptableProjection>>(),
				_registry,
				Options.Create(new EncryptionOptions()), global::Excalibur.Dispatch.UntenantedContext.Instance));

		// The message has to be actionable: it names the declaring type, the property, and what is supported.
		ex.Message.ShouldContain(nameof(UnencryptableProjection));
		ex.Message.ShouldContain(nameof(UnencryptableProjection.Attempts));
		ex.Message.ShouldContain("System.Int32");
		ex.Message.ShouldContain(EncryptedFieldBinding.SupportedTypeNames);
	}

	[Fact]
	public void RejectAnAnnotationOnAPropertyWithNoSetter()
	{
		var ex = Should.Throw<EncryptionException>(() =>
			new EncryptingProjectionStoreDecorator<GetterOnlyProjection>(
				A.Fake<IProjectionStore<GetterOnlyProjection>>(),
				_registry,
				Options.Create(new EncryptionOptions()), global::Excalibur.Dispatch.UntenantedContext.Instance));

		ex.Message.ShouldContain(nameof(GetterOnlyProjection.ReadOnlySecret));

		// Naming the property is NOT enough to identify WHY it was refused. Every refusal this binding
		// raises names the offending property, so an arm asserting only the name passes whichever cause
		// fired — including a cause this test was never written to cover. The binding accumulates all of
		// its reasons into one flat list of prose, so the message text is the only thing that separates
		// them, and that separation has to be asserted or it is not tested.
		ex.Message.ShouldContain(
			"a getter and a setter are both required",
			Case.Sensitive,
			"this arm exists to prove the MISSING-SETTER refusal specifically. Without binding the cause, "
			+ "it stays green when the property is refused for some entirely different reason — an "
			+ "unsupported type, or any refusal added later — and the setter requirement silently stops "
			+ "being covered. The sibling arm above binds its own cause the same way, by asserting "
			+ "System.Int32 and the supported-type list.");
	}

	[Fact]
	public async Task StillStoreARecordWhoseExistingStoredValueIsCorrupt()
	{
		// A WRITER must not be blocked by damage to the value already in the field. Validating on the
		// write path would make a row whose ciphertext was truncated permanently unwritable - and
		// truncation is a write-side cause (Base64 inflates 4/3 and the marker adds six characters, so a
		// column sized for the plaintext is exactly where it happens). The reader still refuses the same
		// value; see RefuseToTreatACorruptEnvelopeAsPlaintext, which is that value on the opposite path.
		var decorator = CreateDecorator();
		var projection = new StringSecretProjection
		{
			Id = "proj-1",
			ApiKey = EncryptedFieldBinding.StringEnvelopePrefix + "!!!not-base64!!!",
		};

		await decorator.UpsertAsync("proj-1", projection, _ct);

		// Marked, so not re-encrypted - and crucially not thrown on.
		A.CallTo(() => _registry.GetPrimary()).MustNotHaveHappened();
	}

	[Fact]
	public async Task RejectAnEnvelopeBeyondTheDecodeCeiling()
	{
		var decorator = CreateDecorator();
		var projection = new StringSecretProjection
		{
			Id = "proj-1",
			ApiKey = EncryptedFieldBinding.StringEnvelopePrefix
				+ new string('A', EncryptedFieldBinding.MaxEncodedEnvelopeLength + 4),
		};
		A.CallTo(() => _innerStore.GetByIdAsync("proj-1", _ct)).Returns(Task.FromResult<StringSecretProjection?>(projection));

		// Rejected on the READ path without decoding it, because decoding is what the size would cost.
		var ex = await Should.ThrowAsync<EncryptionException>(
			() => decorator.GetByIdAsync("proj-1", _ct));
		ex.Message.ShouldContain("without decoding");
	}

}

public sealed class StringSecretProjection
{
	public string Id { get; set; } = string.Empty;

	[EncryptedField]
	public string? ApiKey { get; set; }
}

public sealed class UnencryptableProjection
{
	public string Id { get; set; } = string.Empty;

	[EncryptedField]
	public int Attempts { get; set; }
}

public sealed class GetterOnlyProjection
{
	public string Id { get; set; } = string.Empty;

	[EncryptedField]
	public string ReadOnlySecret => "immutable";
}
