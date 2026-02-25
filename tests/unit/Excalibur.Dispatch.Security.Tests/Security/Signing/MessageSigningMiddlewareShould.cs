// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="MessageSigningMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Signing")]
public sealed class MessageSigningMiddlewareShould
{
    private readonly IMessageSigningService _signingService;
    private readonly ILogger<MessageSigningMiddleware> _logger;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;
    private readonly IMessageResult _successResult;
    private readonly Dictionary<string, object> _contextItems;

    public MessageSigningMiddlewareShould()
    {
        _signingService = A.Fake<IMessageSigningService>();
        _logger = new NullLogger<MessageSigningMiddleware>();
        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        _nextDelegate = A.Fake<DispatchRequestDelegate>();
        _successResult = A.Fake<IMessageResult>();
        _contextItems = new Dictionary<string, object>(StringComparer.Ordinal);

        A.CallTo(() => _successResult.Succeeded).Returns(true);
        A.CallTo(() => _nextDelegate(_message, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        // Wire up Items so extension method TryGetValue works via context.Items
        A.CallTo(() => _context.Items).Returns(_contextItems);
    }

    [Fact]
    public void ImplementIDispatchMiddleware()
    {
        var sut = CreateMiddleware();
        sut.ShouldBeAssignableTo<IDispatchMiddleware>();
    }

    [Fact]
    public void HaveValidationStage()
    {
        var sut = CreateMiddleware();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
    }

    [Fact]
    public void HaveAllMessageKinds()
    {
        var sut = CreateMiddleware();
        sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
    }

    [Fact]
    public void BePublicAndSealed()
    {
        typeof(MessageSigningMiddleware).IsPublic.ShouldBeTrue();
        typeof(MessageSigningMiddleware).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenSigningServiceIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageSigningMiddleware(null!, Microsoft.Extensions.Options.Options.Create(new SigningOptions()), _logger));
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageSigningMiddleware(_signingService, null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageSigningMiddleware(_signingService, Microsoft.Extensions.Options.Options.Create(new SigningOptions()), null!));
    }

    [Fact]
    public async Task SkipSigningWhenDisabled()
    {
        // Arrange
        var options = new SigningOptions { Enabled = false };
        var sut = new MessageSigningMiddleware(_signingService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        var sut = CreateMiddleware();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(null!, _context, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenContextIsNull()
    {
        var sut = CreateMiddleware();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, null!, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenNextDelegateIsNull()
    {
        var sut = CreateMiddleware();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessOutgoingMessageAndSign()
    {
        // Arrange — no "MessageDirection" in Items means outgoing
        var sut = CreateMiddleware();

        A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult("signature-value"));

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task VerifyIncomingMessageSignature()
    {
        // Arrange — set incoming direction + signature in Items dictionary
        var options = new SigningOptions { Enabled = true, RequireValidSignature = true };
        var sut = new MessageSigningMiddleware(_signingService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        _contextItems["MessageDirection"] = "Incoming";
        _contextItems["MessageSignature"] = "valid-signature";

        A.CallTo(() => _signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(true));

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        A.CallTo(() => _signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectIncomingMessageWithInvalidSignature()
    {
        // Arrange
        var options = new SigningOptions { Enabled = true, RequireValidSignature = true };
        var sut = new MessageSigningMiddleware(_signingService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        _contextItems["MessageDirection"] = "Incoming";
        _contextItems["MessageSignature"] = "invalid-signature";

        A.CallTo(() => _signingService.VerifySignatureAsync(A<string>._, A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult(false));

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnFailedResultOnSigningException()
    {
        // Arrange — no direction means outgoing
        var sut = CreateMiddleware();

        A.CallTo(() => _nextDelegate(_message, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        A.CallTo(() => _signingService.SignMessageAsync(A<string>._, A<SigningContext>._, A<CancellationToken>._))
            .Throws(new SigningException("signing error"));

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
    }

    private MessageSigningMiddleware CreateMiddleware()
    {
        return new MessageSigningMiddleware(
            _signingService,
            Microsoft.Extensions.Options.Options.Create(new SigningOptions { Enabled = true }),
            _logger);
    }
}
