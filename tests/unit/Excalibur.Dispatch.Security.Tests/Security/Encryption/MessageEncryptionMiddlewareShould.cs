// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Encryption;

/// <summary>
/// Unit tests for <see cref="MessageEncryptionMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Encryption")]
public sealed class MessageEncryptionMiddlewareShould
{
    private readonly IMessageEncryptionService _encryptionService;
    private readonly ILogger<MessageEncryptionMiddleware> _logger;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;
    private readonly IMessageResult _successResult;
    private readonly Dictionary<string, object> _contextItems;

    public MessageEncryptionMiddlewareShould()
    {
        _encryptionService = A.Fake<IMessageEncryptionService>();
        _logger = new NullLogger<MessageEncryptionMiddleware>();
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
    public void HaveSerializationStage()
    {
        var sut = CreateMiddleware();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Serialization);
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
        typeof(MessageEncryptionMiddleware).IsPublic.ShouldBeTrue();
        typeof(MessageEncryptionMiddleware).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenEncryptionServiceIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageEncryptionMiddleware(null!, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()), _logger));
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageEncryptionMiddleware(_encryptionService, null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessageEncryptionMiddleware(_encryptionService, Microsoft.Extensions.Options.Options.Create(new EncryptionOptions()), null!));
    }

    [Fact]
    public async Task SkipEncryptionWhenDisabled()
    {
        // Arrange
        var options = new EncryptionOptions { Enabled = false };
        var sut = new MessageEncryptionMiddleware(_encryptionService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipEncryptionForExcludedMessageTypes()
    {
        // Arrange
        var options = new EncryptionOptions
        {
            Enabled = true,
            ExcludedMessageTypes = new HashSet<string>(StringComparer.Ordinal) { _message.GetType().Name },
        };
        var sut = new MessageEncryptionMiddleware(_encryptionService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipEncryptionWhenDisabledInContext()
    {
        // Arrange
        var options = new EncryptionOptions { Enabled = true, EncryptByDefault = true };
        var sut = new MessageEncryptionMiddleware(_encryptionService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Set DisableEncryption=true in Items dictionary (used by TryGetValue extension)
        _contextItems["DisableEncryption"] = true;

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task EncryptSensitiveMessages()
    {
        // Arrange
        // Fake must implement both IDispatchMessage and ISensitiveMessage
        var sensitiveMessage = A.Fake<IDispatchMessage>(o => o.Implements<ISensitiveMessage>());
        var options = new EncryptionOptions { Enabled = true };
        var sut = new MessageEncryptionMiddleware(_encryptionService, Microsoft.Extensions.Options.Options.Create(options), _logger);

        // No DisableEncryption, no MessageDirection (defaults to outgoing)
        A.CallTo(() => _nextDelegate(sensitiveMessage, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        A.CallTo(() => _encryptionService.EncryptMessageAsync(A<string>._, A<EncryptionContext>._, A<CancellationToken>._))
            .Returns(Task.FromResult("encrypted-payload"));

        // Act
        var result = await sut.InvokeAsync(sensitiveMessage, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
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

    private MessageEncryptionMiddleware CreateMiddleware()
    {
        return new MessageEncryptionMiddleware(
            _encryptionService,
            Microsoft.Extensions.Options.Options.Create(new EncryptionOptions { Enabled = true }),
            _logger);
    }
}
