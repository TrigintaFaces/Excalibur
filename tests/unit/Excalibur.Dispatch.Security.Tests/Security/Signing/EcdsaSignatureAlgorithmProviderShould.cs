// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Security;

namespace Excalibur.Dispatch.Security.Tests.Signing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class EcdsaSignatureAlgorithmProviderShould
{
	private readonly EcdsaSignatureAlgorithmProvider _sut = new();

	// -- SupportsAlgorithm --

	[Fact]
	public void SupportEcdsaSha256Algorithm()
	{
		_sut.SupportsAlgorithm(SigningAlgorithm.ECDSASHA256).ShouldBeTrue();
	}

	[Theory]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.Unknown)]
	public void NotSupportNonEcdsaAlgorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeFalse();
	}

	// -- SignAsync --

	[Fact]
	public async Task SignAndProduceNonEmptySignature()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var privateKey = ecdsa.ExportPkcs8PrivateKey();
		var data = Encoding.UTF8.GetBytes("test message");

		var signature = await _sut.SignAsync(data, privateKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None);

		signature.ShouldNotBeNull();
		signature.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignDataIsNull()
	{
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(null!, key, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignKeyMaterialIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(data, null!, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowSigningExceptionForInvalidKeyMaterial()
	{
		var data = Encoding.UTF8.GetBytes("test message");
		var invalidKey = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		await Should.ThrowAsync<SigningException>(
			() => _sut.SignAsync(data, invalidKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	// -- VerifyAsync --

	[Fact]
	public async Task VerifyValidSignatureReturnsTrue()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var privateKey = ecdsa.ExportPkcs8PrivateKey();
		var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("test message for verification");

		var signature = await _sut.SignAsync(data, privateKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None);
		var isValid = await _sut.VerifyAsync(data, signature, publicKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None);

		isValid.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyTamperedDataReturnsFalse()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var privateKey = ecdsa.ExportPkcs8PrivateKey();
		var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("original message");

		var signature = await _sut.SignAsync(data, privateKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None);
		var tamperedData = Encoding.UTF8.GetBytes("tampered message");
		var isValid = await _sut.VerifyAsync(tamperedData, signature, publicKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None);

		isValid.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenVerifyDataIsNull()
	{
		var sig = new byte[64];
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(null!, sig, key, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenVerifySignatureIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(data, null!, key, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowVerificationExceptionForInvalidPublicKey()
	{
		var data = Encoding.UTF8.GetBytes("test message");
		var signature = new byte[64];
		var invalidKey = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		await Should.ThrowAsync<VerificationException>(
			() => _sut.VerifyAsync(data, signature, invalidKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None));
	}

	// -- documented curve strength is enforced, not merely described --------------------------------
	//
	// The published algorithm table names P-256 as the minimum. The curve is carried by the consumer's
	// key rather than chosen here, so these arms are what make that a guarantee instead of a hope.

	[Fact]
	public async Task RejectSigningWithACurveWeakerThanTheDocumentedMinimum()
	{
		using var weak = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP224"));
		var privateKey = weak.ExportPkcs8PrivateKey();
		var data = Encoding.UTF8.GetBytes("test message");

		var error = await Should.ThrowAsync<SigningException>(
			() => _sut.SignAsync(data, privateKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None));

		error.Message.ShouldContain("P-256", Case.Sensitive);
	}

	[Fact]
	public async Task RejectVerifyingWithACurveWeakerThanTheDocumentedMinimum()
	{
		using var weak = ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP224"));
		var publicKey = weak.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("test message");
		var signature = weak.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

		var error = await Should.ThrowAsync<VerificationException>(
			() => _sut.VerifyAsync(data, signature, publicKey, SigningAlgorithm.ECDSASHA256, CancellationToken.None));

		error.Message.ShouldContain("P-256", Case.Sensitive);
	}

	// LIVENESS. A guard that rejected everything would satisfy the two arms above, so a curve at and
	// above the minimum must still round-trip.
	[Theory]
	[InlineData(256)]
	[InlineData(384)]
	[InlineData(521)]
	public async Task AcceptCurvesAtOrAboveTheDocumentedMinimum(int keySizeInBits)
	{
		var curve = keySizeInBits switch
		{
			256 => ECCurve.NamedCurves.nistP256,
			384 => ECCurve.NamedCurves.nistP384,
			_ => ECCurve.NamedCurves.nistP521,
		};

		using var ecdsa = ECDsa.Create(curve);
		var data = Encoding.UTF8.GetBytes("test message");

		var signature = await _sut.SignAsync(
			data, ecdsa.ExportPkcs8PrivateKey(), SigningAlgorithm.ECDSASHA256, CancellationToken.None);
		var verified = await _sut.VerifyAsync(
			data, signature, ecdsa.ExportSubjectPublicKeyInfo(), SigningAlgorithm.ECDSASHA256,
			CancellationToken.None);

		verified.ShouldBeTrue();
	}
}
