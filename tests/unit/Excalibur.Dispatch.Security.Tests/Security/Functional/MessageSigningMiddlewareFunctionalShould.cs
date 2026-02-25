// Functional tests for MessageSigningMiddleware — incoming verification, outgoing signing, disabled bypass, tenant override

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

#pragma warning disable CA2012

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class MessageSigningMiddlewareFunctionalShould
{
    private static MessageSigningMiddleware CreateMiddleware(
        IMessageSigningService? signingService = null,
        SigningOptions? opts = null)
    {
        signingService ??= A.Fake<IMessageSigningService>();
        opts ??= new SigningOptions
        {
            Enabled = true,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "default-key",
            RequireValidSignature = true,
        };

        return new MessageSigningMiddleware(
            signingService,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<MessageSigningMiddleware>.Instance);
    }

    private static (IDispatchMessage Message, IMessageContext Context, DispatchRequestDelegate Next, IMessageResult SuccessResult)
        CreatePipelineFakes(string? direction = null, string? existingSignature = null)
    {
        var message = A.Fake<IDispatchMessage>();
        var items = new Dictionary<string, object>();
        if (direction != null)
        {
            items["MessageDirection"] = direction;
        }

        if (existingSignature != null)
        {
            items["MessageSignature"] = existingSignature;
        }

        var properties = new Dictionary<string, object?>();

        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(properties);

        var successResult = A.Fake<IMessageResult>();
        A.CallTo(() => successResult.Succeeded).Returns(true);

        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(successResult);

        return (message, context, next, successResult);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var middleware = CreateMiddleware(opts: new SigningOptions { Enabled = false });
        var (message, context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.ShouldBe(successResult);
    }

    [Fact]
    public async Task SignOutgoingMessageAfterProcessing()
    {
        var signingService = A.Fake<IMessageSigningService>();
        A.CallTo(() => signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns("test-signature-base64");

        var middleware = CreateMiddleware(signingService);
        var (message, context, next, _) = CreatePipelineFakes(direction: "Outgoing");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        // Verify signing was called
        A.CallTo(() => signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        // Verify signature stored in context properties
        context.Properties["MessageSignature"].ShouldBe("test-signature-base64");
        context.Properties["SignatureAlgorithm"].ShouldNotBeNull();
        context.Properties["SignedAt"].ShouldNotBeNull();
    }

    [Fact]
    public async Task VerifyIncomingMessageSignature()
    {
        var signingService = A.Fake<IMessageSigningService>();
        A.CallTo(() => signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns(true);

        var middleware = CreateMiddleware(signingService);
        var (message, context, next, successResult) = CreatePipelineFakes(direction: "Incoming", existingSignature: "valid-sig");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        A.CallTo(() => signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectIncomingMessageWithInvalidSignature()
    {
        var signingService = A.Fake<IMessageSigningService>();
        A.CallTo(() => signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns(false);

        var middleware = CreateMiddleware(signingService, new SigningOptions
        {
            Enabled = true,
            RequireValidSignature = true,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "key",
        });
        var (message, context, next, _) = CreatePipelineFakes(direction: "Incoming", existingSignature: "bad-sig");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectIncomingMessageWithMissingSignatureWhenRequired()
    {
        var signingService = A.Fake<IMessageSigningService>();
        var middleware = CreateMiddleware(signingService, new SigningOptions
        {
            Enabled = true,
            RequireValidSignature = true,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "key",
        });
        // Incoming message with no signature
        var (message, context, next, _) = CreatePipelineFakes(direction: "Incoming");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task AllowIncomingMessageWithMissingSignatureWhenNotRequired()
    {
        var signingService = A.Fake<IMessageSigningService>();
        var middleware = CreateMiddleware(signingService, new SigningOptions
        {
            Enabled = true,
            RequireValidSignature = false,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "key",
        });
        var (message, context, next, successResult) = CreatePipelineFakes(direction: "Incoming");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task NotSignOutgoingMessageOnProcessingFailure()
    {
        var signingService = A.Fake<IMessageSigningService>();

        var middleware = CreateMiddleware(signingService);

        var message = A.Fake<IDispatchMessage>();
        var items = new Dictionary<string, object> { ["MessageDirection"] = "Outgoing" };
        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(new Dictionary<string, object?>());

        var failedResult = A.Fake<IMessageResult>();
        A.CallTo(() => failedResult.Succeeded).Returns(false);
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(failedResult);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        // Signing should NOT have been called
        A.CallTo(() => signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleSigningExceptionGracefully()
    {
        var signingService = A.Fake<IMessageSigningService>();
        A.CallTo(() => signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .ThrowsAsync(new SigningException("Key not found"));

        var middleware = CreateMiddleware(signingService);
        var (message, context, next, _) = CreatePipelineFakes(direction: "Outgoing");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void HaveCorrectStage()
    {
        var middleware = CreateMiddleware();
        middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
    }

    [Fact]
    public void HaveCorrectApplicableMessageKinds()
    {
        var middleware = CreateMiddleware();
        middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
    }

    [Fact]
    public async Task ThrowOnNullMessage()
    {
        var middleware = CreateMiddleware();
        var (_, context, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullContext()
    {
        var middleware = CreateMiddleware();
        var (message, _, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullNextDelegate()
    {
        var middleware = CreateMiddleware();
        var (message, context, _, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task UseTenantAlgorithmOverrideForSigningContext()
    {
        var signingService = A.Fake<IMessageSigningService>();
        A.CallTo(() => signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns("sig");

        var signingOptions = new SigningOptions
        {
            Enabled = true,
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
            DefaultKeyId = "key",
        };
        signingOptions.TenantAlgorithms["premium-tenant"] = SigningAlgorithm.HMACSHA512;
        var middleware = CreateMiddleware(signingService, signingOptions);

        var message = A.Fake<IDispatchMessage>();
        var items = new Dictionary<string, object>
        {
            ["MessageDirection"] = "Outgoing",
            ["TenantId"] = "premium-tenant",
        };
        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(new Dictionary<string, object?>());

        var successResult = A.Fake<IMessageResult>();
        A.CallTo(() => successResult.Succeeded).Returns(true);
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(successResult);

        await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        // Verify signing was called with SHA512 for the premium tenant
        A.CallTo(() => signingService.SignMessageAsync(
            A<string>._,
            A<SigningContext>.That.Matches(sc => sc.Algorithm == SigningAlgorithm.HMACSHA512),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
