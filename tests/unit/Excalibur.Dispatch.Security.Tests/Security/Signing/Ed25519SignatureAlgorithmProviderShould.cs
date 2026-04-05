// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text;

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Signing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class Ed25519SignatureAlgorithmProviderShould
{
	private readonly Ed25519SignatureAlgorithmProvider _sut = new();

	[Fact]
	public void SupportEd25519Algorithm()
	{
		_sut.SupportsAlgorithm(SigningAlgorithm.Ed25519).ShouldBeTrue();
	}

	[Theory]
	[InlineData(SigningAlgorithm.HMACSHA256)]
	[InlineData(SigningAlgorithm.ECDSASHA256)]
	[InlineData(SigningAlgorithm.Unknown)]
	public void NotSupportNonEd25519Algorithms(SigningAlgorithm algorithm)
	{
		_sut.SupportsAlgorithm(algorithm).ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowPlatformNotSupportedExceptionOnSign()
	{
		var data = Encoding.UTF8.GetBytes("test");
		var key = new byte[32];

		await Should.ThrowAsync<PlatformNotSupportedException>(
			() => _sut.SignAsync(data, key, SigningAlgorithm.Ed25519, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowPlatformNotSupportedExceptionOnVerify()
	{
		var data = Encoding.UTF8.GetBytes("test");
		var sig = new byte[64];
		var key = new byte[32];

		await Should.ThrowAsync<PlatformNotSupportedException>(
			() => _sut.VerifyAsync(data, sig, key, SigningAlgorithm.Ed25519, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionBeforePlatformCheckOnSignNullData()
	{
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SignAsync(null!, key, SigningAlgorithm.Ed25519, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionBeforePlatformCheckOnVerifyNullData()
	{
		var sig = new byte[64];
		var key = new byte[32];

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.VerifyAsync(null!, sig, key, SigningAlgorithm.Ed25519, CancellationToken.None));
	}
}
