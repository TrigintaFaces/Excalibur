// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="HmacMessageSigningService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Signing")]
public sealed class HmacMessageSigningServiceShould : IDisposable
{
    private static readonly byte[] TestKey = new byte[32];
    private readonly IKeyProvider _keyProvider;
    private readonly ILogger<HmacMessageSigningService> _logger;
    private readonly HmacMessageSigningService _sut;

    public HmacMessageSigningServiceShould()
    {
        Array.Fill(TestKey, (byte)0xAB);
        _keyProvider = A.Fake<IKeyProvider>();
        _logger = new NullLogger<HmacMessageSigningService>();

        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(TestKey));

        _sut = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(new SigningOptions()),
            _keyProvider,
            _logger);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void ImplementIMessageSigningService()
    {
        _sut.ShouldBeAssignableTo<IMessageSigningService>();
    }

    [Fact]
    public void ImplementIDisposable()
    {
        _sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void BePublicAndSealed()
    {
        typeof(HmacMessageSigningService).IsPublic.ShouldBeTrue();
        typeof(HmacMessageSigningService).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(null!, _keyProvider, _logger));
    }

    [Fact]
    public void ThrowWhenKeyProviderIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(Microsoft.Extensions.Options.Options.Create(new SigningOptions()), null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(Microsoft.Extensions.Options.Options.Create(new SigningOptions()), _keyProvider, null!));
    }

    [Fact]
    public async Task SignStringContentWithHmacSha256()
    {
        // Arrange
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };

        // Act
        var signature = await _sut.SignMessageAsync("hello world", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignStringContentWithHmacSha512()
    {
        // Arrange
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA512, Format = SignatureFormat.Base64 };

        // Act
        var signature = await _sut.SignMessageAsync("hello world", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignBytesContent()
    {
        // Arrange
        var content = "test content"u8.ToArray();
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

        // Act
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeEmpty();
        signature.Length.ShouldBe(32); // HMAC-SHA256 = 32 bytes
    }

    [Fact]
    public async Task ProduceDifferentSignaturesForHmacSha256AndSha512()
    {
        // Arrange
        var content = "same content";
        var ctx256 = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };
        var ctx512 = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA512, Format = SignatureFormat.Base64 };

        // Act
        var sig256 = await _sut.SignMessageAsync(content, ctx256, CancellationToken.None);
        var sig512 = await _sut.SignMessageAsync(content, ctx512, CancellationToken.None);

        // Assert
        sig256.ShouldNotBe(sig512);
    }

    [Fact]
    public async Task VerifyValidSignature()
    {
        // Arrange
        var content = "hello world";
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Act
        var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectInvalidSignature()
    {
        // Arrange
        var content = "hello world";
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };
        var invalidSignature = Convert.ToBase64String(new byte[32]);

        // Act
        var isValid = await _sut.VerifySignatureAsync(content, invalidSignature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyBytesSignature()
    {
        // Arrange
        var content = "test bytes"u8.ToArray();
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Act
        var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task SupportHexSignatureFormat()
    {
        // Arrange
        var content = "hex test";
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Hex };

        // Act
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrWhiteSpace();
        // Hex string of 32 bytes = 64 hex characters
        signature.Length.ShouldBe(64);
    }

    [Fact]
    public async Task CreateSignedMessage()
    {
        // Arrange
        var content = "message content";
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64, KeyId = "key-1" };

        // Act
        var signedMessage = await _sut.CreateSignedMessageAsync(content, context, CancellationToken.None);

        // Assert
        signedMessage.ShouldNotBeNull();
        signedMessage.Content.ShouldBe(content);
        signedMessage.Signature.ShouldNotBeNullOrWhiteSpace();
        signedMessage.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        signedMessage.KeyId.ShouldBe("key-1");
        signedMessage.SignedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task ValidateSignedMessage()
    {
        // Arrange
        var content = "message content";
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };
        var signedMessage = await _sut.CreateSignedMessageAsync(content, context, CancellationToken.None);

        // Act
        var validatedContent = await _sut.ValidateSignedMessageAsync(signedMessage, context, CancellationToken.None);

        // Assert
        validatedContent.ShouldBe(content);
    }

    [Fact]
    public async Task ReturnNullForExpiredSignedMessage()
    {
        // Arrange
        var options = new SigningOptions { MaxSignatureAgeMinutes = 1 };
        using var sut = new HmacMessageSigningService(Microsoft.Extensions.Options.Options.Create(options), _keyProvider, _logger);

        var signedMessage = new SignedMessage
        {
            Content = "old content",
            Signature = "dummy",
            Algorithm = SigningAlgorithm.HMACSHA256,
            SignedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256, Format = SignatureFormat.Base64 };

        // Act
        var result = await sut.ValidateSignedMessageAsync(signedMessage, context, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ThrowSigningExceptionOnKeyProviderFailure()
    {
        // Arrange
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Key not found"));

        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

        // Act & Assert
        await Should.ThrowAsync<SigningException>(async () =>
            await _sut.SignMessageAsync("content"u8.ToArray(), context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenSigningNullStringContent()
    {
        // Arrange
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.SignMessageAsync((string)null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenSigningNullBytesContent()
    {
        // Arrange
        var context = new SigningContext { Algorithm = SigningAlgorithm.HMACSHA256 };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.SignMessageAsync((byte[])null!, context, CancellationToken.None));
    }

    [Fact]
    public void DisposeWithoutException()
    {
        // Arrange
        using var sut = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(new SigningOptions()), _keyProvider, _logger);

        // Act & Assert
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void DisposeMultipleTimesWithoutException()
    {
        // Arrange
        var sut = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(new SigningOptions()), _keyProvider, _logger);

        // Act & Assert
        Should.NotThrow(() =>
        {
            sut.Dispose();
            sut.Dispose();
        });
    }
}
