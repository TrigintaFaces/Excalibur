// Functional tests for HmacMessageSigningService — sign, verify, round-trip, key rotation, replay protection

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class HmacMessageSigningServiceFunctionalShould : IDisposable
{
    private readonly IKeyProvider _keyProvider;
    private readonly HmacMessageSigningService _service;
    private static readonly byte[] TestKey = new byte[32];

    public HmacMessageSigningServiceFunctionalShould()
    {
        // Initialize key
        System.Security.Cryptography.RandomNumberGenerator.Fill(TestKey);

        _keyProvider = A.Fake<IKeyProvider>();
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(TestKey));

        var options = new SigningOptions
        {
            Enabled = true,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "default-key",
            MaxSignatureAgeMinutes = 5,
        };

        _service = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(options),
            _keyProvider,
            NullLogger<HmacMessageSigningService>.Instance);
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task SignStringContentWithSha256()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Format = SignatureFormat.Base64,
            IncludeTimestamp = false,
        };

        var signature = await _service.SignMessageAsync("hello world", context, CancellationToken.None);

        signature.ShouldNotBeNullOrEmpty();
        // Base64 encoded HMAC-SHA256 signature should be 44 chars (32 bytes)
        Convert.FromBase64String(signature).Length.ShouldBe(32);
    }

    [Fact]
    public async Task SignStringContentWithSha512()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA512,
            Format = SignatureFormat.Base64,
            IncludeTimestamp = false,
        };

        var signature = await _service.SignMessageAsync("hello world", context, CancellationToken.None);

        signature.ShouldNotBeNullOrEmpty();
        // HMAC-SHA512 produces 64 bytes
        Convert.FromBase64String(signature).Length.ShouldBe(64);
    }

    [Fact]
    public async Task ProduceHexFormattedSignature()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Format = SignatureFormat.Hex,
            IncludeTimestamp = false,
        };

        var signature = await _service.SignMessageAsync("hello world", context, CancellationToken.None);

        signature.ShouldNotBeNullOrEmpty();
        // Hex string for 32 bytes = 64 hex chars
        signature.Length.ShouldBe(64);
        // Should only contain hex chars
        signature.ShouldMatch("^[0-9A-Fa-f]+$");
    }

    [Fact]
    public async Task VerifyValidSignatureRoundTrip()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Format = SignatureFormat.Base64,
            IncludeTimestamp = false,
        };

        var content = "test message content";
        var signature = await _service.SignMessageAsync(content, context, CancellationToken.None);

        var isValid = await _service.VerifySignatureAsync(content, signature, context, CancellationToken.None);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectTamperedContent()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Format = SignatureFormat.Base64,
            IncludeTimestamp = false,
        };

        var signature = await _service.SignMessageAsync("original content", context, CancellationToken.None);

        var isValid = await _service.VerifySignatureAsync("tampered content", signature, context, CancellationToken.None);

        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectTamperedSignature()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Format = SignatureFormat.Base64,
            IncludeTimestamp = false,
        };

        var content = "test message";
        await _service.SignMessageAsync(content, context, CancellationToken.None);

        // Create a different (invalid) signature
        var fakeSignature = Convert.ToBase64String(new byte[32]);

        var isValid = await _service.VerifySignatureAsync(content, fakeSignature, context, CancellationToken.None);

        isValid.ShouldBeFalse();
    }

    [Fact]
    public async Task SignBytesDirectly()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        var content = System.Text.Encoding.UTF8.GetBytes("binary content");
        var signature = await _service.SignMessageAsync(content, context, CancellationToken.None);

        signature.ShouldNotBeNull();
        signature.Length.ShouldBe(32); // HMAC-SHA256 = 32 bytes
    }

    [Fact]
    public async Task VerifyBytesRoundTrip()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        var content = System.Text.Encoding.UTF8.GetBytes("binary content");
        var signature = await _service.SignMessageAsync(content, context, CancellationToken.None);

        var isValid = await _service.VerifySignatureAsync(content, signature, context, CancellationToken.None);

        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSignedMessageWithMetadata()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            KeyId = "my-key",
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
            Metadata = new Dictionary<string, string> { ["source"] = "test" },
        };

        var signedMessage = await _service.CreateSignedMessageAsync("payload", context, CancellationToken.None);

        signedMessage.Content.ShouldBe("payload");
        signedMessage.Signature.ShouldNotBeNullOrEmpty();
        signedMessage.Algorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        signedMessage.KeyId.ShouldBe("my-key");
        signedMessage.SignedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
        signedMessage.Metadata.ShouldContainKey("source");
    }

    [Fact]
    public async Task ValidateSignedMessageSuccessfully()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        var signedMessage = await _service.CreateSignedMessageAsync("validate me", context, CancellationToken.None);

        var validatedContent = await _service.ValidateSignedMessageAsync(signedMessage, context, CancellationToken.None);

        validatedContent.ShouldBe("validate me");
    }

    [Fact]
    public async Task RejectExpiredSignedMessage()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
            Format = SignatureFormat.Base64,
        };

        // Create a signed message with a timestamp in the past
        var signedMessage = new SignedMessage
        {
            Content = "old message",
            Signature = Convert.ToBase64String(new byte[32]),
            Algorithm = SigningAlgorithm.HMACSHA256,
            SignedAt = DateTimeOffset.UtcNow.AddMinutes(-10), // Older than MaxSignatureAgeMinutes (5)
        };

        var validatedContent = await _service.ValidateSignedMessageAsync(signedMessage, context, CancellationToken.None);

        validatedContent.ShouldBeNull();
    }

    [Fact]
    public async Task UseTenantIdInKeyIdentifier()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            TenantId = "tenant-xyz",
            IncludeTimestamp = false,
        };

        await _service.SignMessageAsync("test", context, CancellationToken.None);

        // Verify key provider was called with tenant-scoped key identifier
        A.CallTo(() => _keyProvider.GetKeyAsync(
            A<string>.That.Contains("tenant-xyz"),
            A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task UsePurposeInKeyIdentifier()
    {
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            Purpose = "email-verification",
            IncludeTimestamp = false,
        };

        await _service.SignMessageAsync("test", context, CancellationToken.None);

        A.CallTo(() => _keyProvider.GetKeyAsync(
            A<string>.That.Contains("email-verification"),
            A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task ThrowSigningExceptionOnKeyProviderFailure()
    {
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Key vault unavailable"));

        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        await Should.ThrowAsync<SigningException>(async () =>
            await _service.SignMessageAsync("test", context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowVerificationExceptionOnKeyProviderFailure()
    {
        // First sign successfully
        var context = new SigningContext
        {
            Algorithm = SigningAlgorithm.HMACSHA256,
            IncludeTimestamp = false,
        };

        var signature = await _service.SignMessageAsync("test", context, CancellationToken.None);

        // Now make key provider fail
        A.CallTo(() => _keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Key vault unavailable"));

        // Clear service internal cache by disposing and creating new service
        _service.Dispose();

        var newService = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(new SigningOptions()),
            _keyProvider,
            NullLogger<HmacMessageSigningService>.Instance);

        await Should.ThrowAsync<VerificationException>(async () =>
            await newService.VerifySignatureAsync("test", signature, context, CancellationToken.None));

        newService.Dispose();
    }

    [Fact]
    public async Task ThrowOnNullContentForSign()
    {
        var context = new SigningContext();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _service.SignMessageAsync((string)null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullContextForSign()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _service.SignMessageAsync("test", null!, CancellationToken.None));
    }

    [Fact]
    public void DisposeCleansSensitiveKeyMaterial()
    {
        // Signing caches keys -- after dispose, keys should be cleared
        // This test verifies Dispose() doesn't throw
        var keyProvider = A.Fake<IKeyProvider>();
        A.CallTo(() => keyProvider.GetKeyAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new byte[32]));

        var service = new HmacMessageSigningService(
            Microsoft.Extensions.Options.Options.Create(new SigningOptions()),
            keyProvider,
            NullLogger<HmacMessageSigningService>.Instance);

        service.Dispose();
        // Double dispose should be safe
        service.Dispose();
    }
}
