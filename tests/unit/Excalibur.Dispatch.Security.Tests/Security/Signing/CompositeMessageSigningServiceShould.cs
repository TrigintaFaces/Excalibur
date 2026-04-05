// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Security;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Security.Tests.Signing;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class CompositeMessageSigningServiceShould : IDisposable
{
	private readonly IKeyProvider _keyProvider = A.Fake<IKeyProvider>();
	private readonly ILogger<CompositeMessageSigningService> _logger = NullLogger<CompositeMessageSigningService>.Instance;

	// -- Constructor null guards --

	[Fact]
	public void ThrowArgumentNullExceptionWhenProvidersIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CompositeMessageSigningService(
				null!,
				MsOptions.Create(new SigningOptions()),
				_keyProvider,
				_logger));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CompositeMessageSigningService(
				Array.Empty<ISignatureAlgorithmProvider>(),
				null!,
				_keyProvider,
				_logger));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenKeyProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CompositeMessageSigningService(
				Array.Empty<ISignatureAlgorithmProvider>(),
				MsOptions.Create(new SigningOptions()),
				null!,
				_logger));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CompositeMessageSigningService(
				Array.Empty<ISignatureAlgorithmProvider>(),
				MsOptions.Create(new SigningOptions()),
				_keyProvider,
				null!));
	}

	// -- SignMessageAsync routing --

	[Fact]
	public async Task RouteSigningToCorrectProviderByAlgorithm()
	{
		var hmacKey = RandomNumberGenerator.GetBytes(32);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(hmacKey);

		var providers = new ISignatureAlgorithmProvider[]
		{
			new HmacSignatureAlgorithmProvider(),
			new EcdsaSignatureAlgorithmProvider(),
		};

		using var sut = new CompositeMessageSigningService(
			providers,
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			IncludeTimestamp = false,
		};

		var signature = await sut.SignMessageAsync(
			Encoding.UTF8.GetBytes("test"), context, CancellationToken.None);

		signature.ShouldNotBeNull();
		signature.Length.ShouldBe(32); // HMAC-SHA256 = 32 bytes
	}

	// -- Unsupported algorithm --

	[Fact]
	public async Task ThrowNotSupportedExceptionForUnregisteredAlgorithm()
	{
		using var sut = new CompositeMessageSigningService(
			Array.Empty<ISignatureAlgorithmProvider>(),
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			IncludeTimestamp = false,
		};

		// NotSupportedException is not wrapped, it propagates through SigningException
		await Should.ThrowAsync<SigningException>(
			() => sut.SignMessageAsync(Encoding.UTF8.GetBytes("test"), context, CancellationToken.None));
	}

	// -- Asymmetric key resolution (":pub" suffix) --

	[Fact]
	public async Task AppendPubSuffixForAsymmetricVerification()
	{
		using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
		var privateKey = ecdsa.ExportPkcs8PrivateKey();
		var publicKey = ecdsa.ExportSubjectPublicKeyInfo();

		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>.That.Not.EndsWith(":pub"), A<CancellationToken>._))
			.Returns(privateKey);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>.That.EndsWith(":pub"), A<CancellationToken>._))
			.Returns(publicKey);

		var providers = new ISignatureAlgorithmProvider[] { new EcdsaSignatureAlgorithmProvider() };

		using var sut = new CompositeMessageSigningService(
			providers,
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.ECDSASHA256,
			IncludeTimestamp = false,
			KeyId = "test-key",
		};

		var data = Encoding.UTF8.GetBytes("asymmetric test");
		var signature = await sut.SignMessageAsync(data, context, CancellationToken.None);
		var isValid = await sut.VerifySignatureAsync(data, signature, context, CancellationToken.None);

		isValid.ShouldBeTrue();

		// Verify that :pub suffix was used for verification key lookup
		A.CallTo(() => _keyProvider.GetKeyAsync(
				A<string>.That.EndsWith(":pub"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	// -- Disposed state --

	[Fact]
	public async Task ThrowObjectDisposedExceptionAfterDispose()
	{
		var sut = new CompositeMessageSigningService(
			new[] { new HmacSignatureAlgorithmProvider() },
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		sut.Dispose();

		var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

		await Should.ThrowAsync<ObjectDisposedException>(
			() => sut.SignMessageAsync("test", context, CancellationToken.None));
	}

	// -- String signing and verification roundtrip --

	[Fact]
	public async Task SignAndVerifyStringContentRoundtrip()
	{
		var hmacKey = RandomNumberGenerator.GetBytes(32);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(hmacKey);

		using var sut = new CompositeMessageSigningService(
			new ISignatureAlgorithmProvider[] { new HmacSignatureAlgorithmProvider() },
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			IncludeTimestamp = false,
		};

		var signature = await sut.SignMessageAsync("hello world", context, CancellationToken.None);
		signature.ShouldNotBeNullOrEmpty();

		var isValid = await sut.VerifySignatureAsync("hello world", signature, context, CancellationToken.None);
		isValid.ShouldBeTrue();
	}

	// -- CreateSignedMessageAsync --

	[Fact]
	public async Task CreateSignedMessageWithCorrectProperties()
	{
		var hmacKey = RandomNumberGenerator.GetBytes(32);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(hmacKey);

		using var sut = new CompositeMessageSigningService(
			new ISignatureAlgorithmProvider[] { new HmacSignatureAlgorithmProvider() },
			MsOptions.Create(new SigningOptions()),
			_keyProvider,
			_logger);

		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			IncludeTimestamp = false,
			KeyId = "my-key",
		};

		var signedMessage = await sut.CreateSignedMessageAsync("payload", context, CancellationToken.None);

		signedMessage.Content.ShouldBe("payload");
		signedMessage.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
		signedMessage.KeyId.ShouldBe("my-key");
		signedMessage.Signature.ShouldNotBeNullOrEmpty();
		signedMessage.SignedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	// -- ValidateSignedMessageAsync null result for expired --

	[Fact]
	public async Task ReturnNullForExpiredSignedMessage()
	{
		var hmacKey = RandomNumberGenerator.GetBytes(32);
		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(hmacKey);

		using var sut = new CompositeMessageSigningService(
			new ISignatureAlgorithmProvider[] { new HmacSignatureAlgorithmProvider() },
			MsOptions.Create(new SigningOptions { MaxSignatureAgeMinutes = 1 }),
			_keyProvider,
			_logger);

		var expiredMessage = new SignedMessage
		{
			Content = "test",
			Signature = "dGVzdA==", // dummy base64
			Algorithm = SigningAlgorithm.HMACSHA256,
			SignedAt = DateTimeOffset.UtcNow.AddMinutes(-10), // 10 minutes old
		};

		var context = new SigningContext { IncludeTimestamp = false };

		var result = await sut.ValidateSignedMessageAsync(expiredMessage, context, CancellationToken.None);

		result.ShouldBeNull();
	}

	public void Dispose()
	{
		// Nothing to dispose in test class itself; SUTs are disposed inline
	}
}
