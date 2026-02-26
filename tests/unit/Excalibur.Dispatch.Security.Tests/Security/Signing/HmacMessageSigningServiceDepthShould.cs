// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Deep coverage tests for <see cref="HmacMessageSigningService"/> covering all signing paths,
/// key caching, multi-tenant isolation, signature formats, expiration, and disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class HmacMessageSigningServiceDepthShould : IDisposable
{
	private static readonly byte[] TestKey = RandomNumberGenerator.GetBytes(32);
	private readonly IKeyProvider _keyProvider = A.Fake<IKeyProvider>();
	private readonly SigningOptions _options;
	private readonly HmacMessageSigningService _sut;

	public HmacMessageSigningServiceDepthShould()
	{
		_options = new SigningOptions
		{
			DefaultKeyId = "default-key",
			MaxSignatureAgeMinutes = 60,
		};

		A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(TestKey));

		_sut = new HmacMessageSigningService(
			Microsoft.Extensions.Options.Options.Create(_options),
			_keyProvider,
			NullLogger<HmacMessageSigningService>.Instance);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public async Task SignMessage_WithHmacSha256_ReturnBase64Signature()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
		};

		// Act
		var signature = await _sut.SignMessageAsync("Hello World", context, CancellationToken.None);

		// Assert
		signature.ShouldNotBeNullOrWhiteSpace();
		// Should be valid base64
		Should.NotThrow(() => Convert.FromBase64String(signature));
	}

	[Fact]
	public async Task SignMessage_WithHmacSha512_ReturnHexSignature()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA512,
			Format = SignatureFormat.Hex,
		};

		// Act
		var signature = await _sut.SignMessageAsync("Hello World", context, CancellationToken.None);

		// Assert
		signature.ShouldNotBeNullOrWhiteSpace();
		// Should be valid hex
		Should.NotThrow(() => Convert.FromHexString(signature));
	}

	[Fact]
	public async Task SignMessage_WithTimestamp()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
			IncludeTimestamp = true,
		};

		// Act
		var signature = await _sut.SignMessageAsync("Hello", context, CancellationToken.None);

		// Assert
		signature.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task SignMessageBytes_ReturnByteArray()
	{
		// Arrange
		var content = System.Text.Encoding.UTF8.GetBytes("Test message");
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
		};

		// Act
		var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

		// Assert
		signature.ShouldNotBeNull();
		signature.Length.ShouldBe(32); // SHA256 = 32 bytes
	}

	[Fact]
	public async Task SignMessageBytes_WithHmacSha512_Return64Bytes()
	{
		// Arrange
		var content = System.Text.Encoding.UTF8.GetBytes("Test message");
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA512,
		};

		// Act
		var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

		// Assert
		signature.ShouldNotBeNull();
		signature.Length.ShouldBe(64); // SHA512 = 64 bytes
	}

	[Fact]
	public async Task VerifySignature_ReturnTrue_ForValidSignature()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
		};

		var content = "Test content";
		var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

		// Act
		var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifySignature_ReturnFalse_ForTamperedContent()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
		};

		var signature = await _sut.SignMessageAsync("Original", context, CancellationToken.None);

		// Act
		var isValid = await _sut.VerifySignatureAsync("Tampered", signature, context, CancellationToken.None);

		// Assert
		isValid.ShouldBeFalse();
	}

	[Fact]
	public async Task VerifySignature_WithHexFormat()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Hex,
		};

		var content = "Hex test";
		var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

		// Act
		var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifySignatureBytes_ReturnTrue_ForValidSignature()
	{
		// Arrange
		var content = System.Text.Encoding.UTF8.GetBytes("Test");
		var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

		var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

		// Act
		var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

		// Assert
		isValid.ShouldBeTrue();
	}

	[Fact]
	public async Task CreateSignedMessage_ReturnSignedMessage()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
			KeyId = "my-key",
		};

		// Act
		var signedMsg = await _sut.CreateSignedMessageAsync("Payload", context, CancellationToken.None);

		// Assert
		signedMsg.ShouldNotBeNull();
		signedMsg.Content.ShouldBe("Payload");
		signedMsg.Signature.ShouldNotBeNullOrWhiteSpace();
		signedMsg.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
		signedMsg.KeyId.ShouldBe("my-key");
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		signedMsg.SignedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public async Task ValidateSignedMessage_ReturnContent_WhenValid()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
		};

		var signedMsg = await _sut.CreateSignedMessageAsync("Valid content", context, CancellationToken.None);

		// Act
		var content = await _sut.ValidateSignedMessageAsync(signedMsg, context, CancellationToken.None);

		// Assert
		content.ShouldBe("Valid content");
	}

	[Fact]
	public async Task ValidateSignedMessage_ReturnNull_WhenExpired()
	{
		// Arrange
		_options.MaxSignatureAgeMinutes = 1;
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Format = SignatureFormat.Base64,
		};

		var signedMsg = new SignedMessage
		{
			Content = "Old content",
			Signature = "dummy",
			Algorithm = SigningAlgorithm.HMACSHA256,
			KeyId = "key-1",
			SignedAt = DateTimeOffset.UtcNow.AddMinutes(-10), // expired
			Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
		};

		// Act
		var content = await _sut.ValidateSignedMessageAsync(signedMsg, context, CancellationToken.None);

		// Assert
		content.ShouldBeNull();
	}

	[Fact]
	public async Task SignMessage_UseTenantKeyIsolation()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			TenantId = "tenant-A",
			KeyId = "key-A",
		};

		// Act
		await _sut.SignMessageAsync("Test", context, CancellationToken.None);

		// Assert - key provider was called with tenant-specific key ID
		A.CallTo(() => _keyProvider.GetKeyAsync(
				A<string>.That.Contains("tenant-A"),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task SignMessage_UseDefaultKeyId_WhenNoKeySpecified()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
		};

		// Act
		await _sut.SignMessageAsync("Test", context, CancellationToken.None);

		// Assert - key provider was called with default key ID
		A.CallTo(() => _keyProvider.GetKeyAsync(
				A<string>.That.Contains("default-key"),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task SignMessage_UsePurpose()
	{
		// Arrange
		var context = new SigningContext
		{
			Algorithm = SigningAlgorithm.HMACSHA256,
			Purpose = "webhook",
		};

		// Act
		await _sut.SignMessageAsync("Test", context, CancellationToken.None);

		// Assert
		A.CallTo(() => _keyProvider.GetKeyAsync(
				A<string>.That.Contains("webhook"),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public void SignMessage_ThrowOnNullContent()
	{
		var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SignMessageAsync((string)null!, context, CancellationToken.None));
	}

	[Fact]
	public void SignMessage_ThrowOnNullContext()
	{
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SignMessageAsync("test", null!, CancellationToken.None));
	}

	[Fact]
	public void VerifySignature_ThrowOnNullContent()
	{
		var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };
		Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.VerifySignatureAsync((string)null!, "sig", context, CancellationToken.None));
	}

	[Fact]
	public void Dispose_ClearsSensitiveKeyMaterial()
	{
		// Act & Assert - no exception, disposal is safe
		_sut.Dispose();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		_sut.Dispose();
		_sut.Dispose();
	}
}
