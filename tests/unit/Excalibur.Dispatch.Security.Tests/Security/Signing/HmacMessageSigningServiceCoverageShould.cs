// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class HmacMessageSigningServiceCoverageShould : IDisposable
{
    private static readonly byte[] TestKey = Encoding.UTF8.GetBytes("test-key-1234567890-abcdefghijkl");
    private readonly IKeyProvider _keyProvider;
    private readonly HmacMessageSigningService _sut;

    public HmacMessageSigningServiceCoverageShould()
    {
        _keyProvider = A.Fake<IKeyProvider>();
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Returns(TestKey);

        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions
        {
            DefaultKeyId = "default-key",
            MaxSignatureAgeMinutes = 5,
        });

        _sut = new HmacMessageSigningService(
            options,
            _keyProvider,
            NullLogger<HmacMessageSigningService>.Instance);
    }

    [Fact]
    public async Task SignMessageWithHmacSha256()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        // Act
        var signature = await _sut.SignMessageAsync("hello world", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SignMessageWithHmacSha512()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA512,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        // Act
        var signature = await _sut.SignMessageAsync("hello world", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SignMessageWithHexFormat()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Hex,
        };

        // Act
        var signature = await _sut.SignMessageAsync("hello world", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrEmpty();
        // Hex strings contain only hex characters
        signature.ShouldAllBe(c => "0123456789ABCDEF".Contains(c));
    }

    [Fact]
    public async Task SignBinaryMessageWithHmacSha256()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("binary content");
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        // Act
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNull();
        signature.Length.ShouldBe(32); // SHA256 produces 32-byte hash
    }

    [Fact]
    public async Task SignBinaryMessageWithHmacSha512()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("binary content");
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA512,
            IncludeTimestamp = false,
        };

        // Act
        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNull();
        signature.Length.ShouldBe(64); // SHA512 produces 64-byte hash
    }

    [Fact]
    public async Task SignMessageWithTimestamp()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = true,
        };

        // Act
        var signature = await _sut.SignMessageAsync("with timestamp", context, CancellationToken.None);

        // Assert
        signature.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifyValidSignatureString()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        var signature = await _sut.SignMessageAsync("hello", context, CancellationToken.None);

        // Act
        var isValid = await _sut.VerifySignatureAsync("hello", signature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyInvalidSignatureString()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        // Act
        var isValid = await _sut.VerifySignatureAsync("hello", Convert.ToBase64String(new byte[32]), context, CancellationToken.None);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyValidSignatureBytes()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("verify me");
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        var signature = await _sut.SignMessageAsync(content, context, CancellationToken.None);

        // Act
        var isValid = await _sut.VerifySignatureAsync(content, signature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyInvalidSignatureBytes()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("verify me");
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        // Act
        var isValid = await _sut.VerifySignatureAsync(content, new byte[32], context, CancellationToken.None);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifySignatureWithHexFormat()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Hex,
        };

        var signature = await _sut.SignMessageAsync("hello", context, CancellationToken.None);

        // Act
        var isValid = await _sut.VerifySignatureAsync("hello", signature, context, CancellationToken.None);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSignedMessage()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            KeyId = "my-key",
            IncludeTimestamp = false,
            Metadata = { ["purpose"] = "test" },
        };

        // Act
        var signedMessage = await _sut.CreateSignedMessageAsync("test content", context, CancellationToken.None);

        // Assert
        signedMessage.ShouldNotBeNull();
        signedMessage.Content.ShouldBe("test content");
        signedMessage.Signature.ShouldNotBeNullOrEmpty();
        signedMessage.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        signedMessage.KeyId.ShouldBe("my-key");
        signedMessage.SignedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        signedMessage.Metadata.ShouldContainKeyAndValue("purpose", "test");
    }

    [Fact]
    public async Task ValidateSignedMessageWithValidSignature()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions
        {
            DefaultKeyId = "default-key",
            MaxSignatureAgeMinutes = 0, // Disable age check
        });
        using var sut = new HmacMessageSigningService(options, _keyProvider, NullLogger<HmacMessageSigningService>.Instance);

        var createContext = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        var signedMessage = await sut.CreateSignedMessageAsync("valid content", createContext, CancellationToken.None);

        var verifyContext = new SigningContext { IncludeTimestamp = false };

        // Act
        var result = await sut.ValidateSignedMessageAsync(signedMessage, verifyContext, CancellationToken.None);

        // Assert
        result.ShouldBe("valid content");
    }

    [Fact]
    public async Task ValidateSignedMessageReturnsNullForExpiredSignature()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions
        {
            DefaultKeyId = "default-key",
            MaxSignatureAgeMinutes = 1,
        });
        using var sut = new HmacMessageSigningService(options, _keyProvider, NullLogger<HmacMessageSigningService>.Instance);

        var signedMessage = new SignedMessage
        {
            Content = "expired content",
            Signature = "dummy",
            Algorithm = SigningAlgorithm.HMACSHA256,
            SignedAt = DateTimeOffset.UtcNow.AddMinutes(-10), // Very old
        };

        var context = new SigningContext { IncludeTimestamp = false };

        // Act
        var result = await sut.ValidateSignedMessageAsync(signedMessage, context, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ThrowSigningExceptionForUnsupportedAlgorithmOnSign()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("test");
        var context = new SigningContext
        {
            Algorithm = (SigningAlgorithm)99, // Unsupported
            IncludeTimestamp = false,
        };

        // Act & Assert
        await Should.ThrowAsync<SigningException>(
            () => _sut.SignMessageAsync(content, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowVerificationExceptionForUnsupportedAlgorithmOnVerify()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("test");
        var context = new SigningContext
        {
            Algorithm = (SigningAlgorithm)99, // Unsupported
            IncludeTimestamp = false,
        };

        // Act & Assert
        await Should.ThrowAsync<VerificationException>(
            () => _sut.VerifySignatureAsync(content, new byte[32], context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContentOnStringSign()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SignMessageAsync((string)null!, new SigningContext(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnStringSign()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SignMessageAsync("hello", null!, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContentOnBinarySign()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SignMessageAsync((byte[])null!, new SigningContext(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnBinarySign()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.SignMessageAsync(new byte[1], null!, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContentOnStringVerify()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.VerifySignatureAsync((string)null!, "sig", new SigningContext(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullSignatureOnStringVerify()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.VerifySignatureAsync("content", (string)null!, new SigningContext(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnStringVerify()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.VerifySignatureAsync("content", "sig", null!, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnBinaryVerify()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.VerifySignatureAsync(new byte[1], new byte[1], null!, CancellationToken.None));
    }

    [Fact]
    public async Task BuildKeyIdentifierWithTenantId()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            TenantId = "tenant-1",
            IncludeTimestamp = false,
        };

        // Act
        var signature = await _sut.SignMessageAsync("test", context, CancellationToken.None);

        // Assert - verify that key provider was called with tenant-qualified key
        A.CallTo(() => _keyProvider.GetKeyAsync(
            A<string>.That.Contains("tenant-1"),
            A<CancellationToken>._)).MustHaveHappened();
        signature.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildKeyIdentifierWithKeyId()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            KeyId = "custom-key",
            IncludeTimestamp = false,
        };

        // Act
        var signature = await _sut.SignMessageAsync("test", context, CancellationToken.None);

        // Assert
        A.CallTo(() => _keyProvider.GetKeyAsync(
            A<string>.That.Contains("custom-key"),
            A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task BuildKeyIdentifierWithPurpose()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Purpose = "encryption",
            IncludeTimestamp = false,
        };

        // Act
        var signature = await _sut.SignMessageAsync("test", context, CancellationToken.None);

        // Assert
        A.CallTo(() => _keyProvider.GetKeyAsync(
            A<string>.That.Contains("encryption"),
            A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task CacheKeysAndReuseOnSubsequentCalls()
    {
        // Arrange
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        // Act - sign twice with same context
        await _sut.SignMessageAsync("first", context, CancellationToken.None);
        await _sut.SignMessageAsync("second", context, CancellationToken.None);

        // Assert - key provider should only be called once (cached)
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void DisposeMultipleTimes()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions());
        var sut = new HmacMessageSigningService(options, _keyProvider, NullLogger<HmacMessageSigningService>.Instance);

        // Act & Assert - should not throw
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(null!, _keyProvider, NullLogger<HmacMessageSigningService>.Instance));
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullKeyProvider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(Microsoft.Extensions.Options.Options.Create(new SigningOptions()), null!, NullLogger<HmacMessageSigningService>.Instance));
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullLogger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new HmacMessageSigningService(Microsoft.Extensions.Options.Options.Create(new SigningOptions()), _keyProvider, null!));
    }

    [Fact]
    public async Task WrapKeyProviderExceptionInSigningException()
    {
        // Arrange
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Key vault unavailable"));

        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions());
        using var sut = new HmacMessageSigningService(options, _keyProvider, NullLogger<HmacMessageSigningService>.Instance);

        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<SigningException>(
            () => sut.SignMessageAsync(new byte[] { 1, 2, 3 }, context, CancellationToken.None));
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task WrapKeyProviderExceptionInVerificationException()
    {
        // Arrange
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Key vault unavailable"));

        var options = Microsoft.Extensions.Options.Options.Create(new SigningOptions());
        using var sut = new HmacMessageSigningService(options, _keyProvider, NullLogger<HmacMessageSigningService>.Instance);

        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<VerificationException>(
            () => sut.VerifySignatureAsync(new byte[] { 1, 2, 3 }, new byte[32], context, CancellationToken.None));
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullSignedMessage()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ValidateSignedMessageAsync(null!, new SigningContext(), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnValidate()
    {
        // Arrange
        var signedMessage = new SignedMessage { Content = "x", Signature = "y" };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ValidateSignedMessageAsync(signedMessage, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContextOnCreateSigned()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CreateSignedMessageAsync("content", null!, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContentOnCreateSigned()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CreateSignedMessageAsync(null!, new SigningContext(), CancellationToken.None));
    }

    public void Dispose() => _sut.Dispose();
}
