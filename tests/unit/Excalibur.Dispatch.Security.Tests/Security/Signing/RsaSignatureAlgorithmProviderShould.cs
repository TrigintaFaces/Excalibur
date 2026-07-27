// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Security;

namespace Excalibur.Dispatch.Security.Tests.Signing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class RsaSignatureAlgorithmProviderShould
{
	private readonly RsaSignatureAlgorithmProvider _sut = new();

	// -- SupportsAlgorithm --

	[Theory]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.RSAPSSSHA256)]
	public void SupportRsaAlgorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeTrue();
	}

	[Theory]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	[InlineData(SigningAlgorithm.ECDSASHA256)]
	[InlineData(SigningAlgorithm.Unknown)]
	public void NotSupportNonRsaAlgorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeFalse();
	}

	// -- SignAsync + VerifyAsync round-trip (wired-AND-tested) --

	[Theory]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.RSAPSSSHA256)]
	public async Task SignAndVerifyValidSignatureReturnsTrue(SigningAlgorithm algorithm)
	{
		using var rsa = RSA.Create(2048);
		var privateKey = rsa.ExportPkcs8PrivateKey();
		var publicKey = rsa.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("test message for verification");

		var signature = await _sut.SignAsync(data, privateKey, algorithm, CancellationToken.None);
		var isValid = await _sut.VerifyAsync(data, signature, publicKey, algorithm, CancellationToken.None);

		signature.Length.ShouldBeGreaterThan(0);
		isValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.RSAPSSSHA256)]
	public async Task VerifyTamperedDataReturnsFalse(SigningAlgorithm algorithm)
	{
		using var rsa = RSA.Create(2048);
		var privateKey = rsa.ExportPkcs8PrivateKey();
		var publicKey = rsa.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("original message");

		var signature = await _sut.SignAsync(data, privateKey, algorithm, CancellationToken.None);
		var tamperedData = Encoding.UTF8.GetBytes("tampered message");
		var isValid = await _sut.VerifyAsync(tamperedData, signature, publicKey, algorithm, CancellationToken.None);

		isValid.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyWithWrongPaddingReturnsFalse()
	{
		using var rsa = RSA.Create(2048);
		var privateKey = rsa.ExportPkcs8PrivateKey();
		var publicKey = rsa.ExportSubjectPublicKeyInfo();
		var data = Encoding.UTF8.GetBytes("padding-sensitive message");

		var pkcs1Signature = await _sut.SignAsync(data, privateKey, SigningAlgorithm.RSASHA256, CancellationToken.None);
		var isValidUnderPss = await _sut.VerifyAsync(data, pkcs1Signature, publicKey, SigningAlgorithm.RSAPSSSHA256, CancellationToken.None);

		isValidUnderPss.ShouldBeFalse();
	}

	// -- Argument / key-material guards --

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignDataIsNull()
	{
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(null!, key, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignKeyMaterialIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(data, null!, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowSigningExceptionForInvalidKeyMaterial()
	{
		var data = Encoding.UTF8.GetBytes("test message");
		var invalidKey = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		await Should.ThrowAsync<SigningException>(
			() => _sut.SignAsync(data, invalidKey, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenVerifyDataIsNull()
	{
		var sig = new byte[256];
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(null!, sig, key, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenVerifySignatureIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(data, null!, key, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowVerificationExceptionForInvalidPublicKey()
	{
		var data = Encoding.UTF8.GetBytes("test message");
		var signature = new byte[256];
		var invalidKey = new byte[] { 0x00, 0x01, 0x02, 0x03 };

		await Should.ThrowAsync<VerificationException>(
			() => _sut.VerifyAsync(data, signature, invalidKey, SigningAlgorithm.RSASHA256, CancellationToken.None));
	}
}
