// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Signing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class HmacSignatureAlgorithmProviderShould
{
	private readonly HmacSignatureAlgorithmProvider _sut = new();

	// -- SupportsAlgorithm --

	[Theory]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.HMACSHA512)]
	public void SupportHmacAlgorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeTrue();
	}

	[Theory]
	[InlineData(SigningAlgorithm.ECDSASHA256)]
	[InlineData(SigningAlgorithm.Ed25519)]
	[InlineData(SigningAlgorithm.RSASHA256)]
	[InlineData(SigningAlgorithm.Unknown)]
	public void NotSupportNonHmacAlgorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeFalse();
	}

	// -- SignAsync HMACSHA256 --

	[Fact]
	public async Task SignWithHmacSha256ProducesDeterministicSignature()
	{
		var key = RandomNumberGenerator.GetBytes(32);
		var data = Encoding.UTF8.GetBytes("test message");

		var sig1 = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);
		var sig2 = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);

		sig1.ShouldBe(sig2);
		sig1.Length.ShouldBe(32); // SHA-256 = 32 bytes
	}

	// -- SignAsync HMACSHA512 --

	[Fact]
	public async Task SignWithHmacSha512ProducesDeterministicSignature()
	{
		var key = RandomNumberGenerator.GetBytes(64);
		var data = Encoding.UTF8.GetBytes("test message");

		var sig1 = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA512, CancellationToken.None);
		var sig2 = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA512, CancellationToken.None);

		sig1.ShouldBe(sig2);
		sig1.Length.ShouldBe(64); // SHA-512 = 64 bytes
	}

	// -- SignAsync null guards --

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignDataIsNull()
	{
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(null!, key, SigningAlgorithm.HMACSHA256, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenSignKeyMaterialIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(data, null!, SigningAlgorithm.HMACSHA256, CancellationToken.None));
	}

	// -- VerifyAsync --

	[Fact]
	public async Task VerifyValidHmacSha256SignatureReturnsTrue()
	{
		var key = RandomNumberGenerator.GetBytes(32);
		var data = Encoding.UTF8.GetBytes("test message for verification");

		var signature = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);
		var isValid = await _sut.VerifyAsync(data, signature, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);

		isValid.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyTamperedDataReturnsFalse()
	{
		var key = RandomNumberGenerator.GetBytes(32);
		var data = Encoding.UTF8.GetBytes("original message");

		var signature = await _sut.SignAsync(data, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);
		var tamperedData = Encoding.UTF8.GetBytes("tampered message");
		var isValid = await _sut.VerifyAsync(tamperedData, signature, key, SigningAlgorithm.HMACSHA256, CancellationToken.None);

		isValid.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifyWithWrongKeyReturnsFalse()
	{
		var key1 = RandomNumberGenerator.GetBytes(32);
		var key2 = RandomNumberGenerator.GetBytes(32);
		var data = Encoding.UTF8.GetBytes("test message");

		var signature = await _sut.SignAsync(data, key1, SigningAlgorithm.HMACSHA256, CancellationToken.None);
		var isValid = await _sut.VerifyAsync(data, signature, key2, SigningAlgorithm.HMACSHA256, CancellationToken.None);

		isValid.ShouldBeFalse();
	}

	// -- VerifyAsync null guards --

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenVerifySignatureIsNull()
	{
		var data = Encoding.UTF8.GetBytes("test");
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(data, null!, key, SigningAlgorithm.HMACSHA256, CancellationToken.None));
	}
}
