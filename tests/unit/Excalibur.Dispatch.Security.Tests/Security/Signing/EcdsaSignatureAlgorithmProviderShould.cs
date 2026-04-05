// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Security;

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
	[InlineData(SigningAlgorithm.Ed25519)]
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
}
