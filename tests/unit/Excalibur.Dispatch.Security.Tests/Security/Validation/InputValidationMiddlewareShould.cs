// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Unit tests for <see cref="InputValidationMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Validation")]
public sealed class InputValidationMiddlewareShould
{
    private readonly ILogger<InputValidationMiddleware> _logger;
    private readonly ISecurityEventLogger _securityEventLogger;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;
    private readonly IMessageResult _successResult;

    public InputValidationMiddlewareShould()
    {
        _logger = new NullLogger<InputValidationMiddleware>();
        _securityEventLogger = A.Fake<ISecurityEventLogger>();
        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        _nextDelegate = A.Fake<DispatchRequestDelegate>();
        _successResult = A.Fake<IMessageResult>();

        A.CallTo(() => _successResult.Succeeded).Returns(true);
        A.CallTo(() => _nextDelegate(_message, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        // Setup default context values
        A.CallTo(() => _context.CorrelationId).Returns(Guid.NewGuid().ToString());
        A.CallTo(() => _context.MessageId).Returns(Guid.NewGuid().ToString());
        A.CallTo(() => _context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow);
        A.CallTo(() => _context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

        A.CallTo(() => _securityEventLogger.LogSecurityEventAsync(
            A<SecurityEventType>._, A<string>._, A<SecuritySeverity>._, A<CancellationToken>._, A<IMessageContext?>._))
            .Returns(Task.CompletedTask);
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
    public void BePublicAndSealed()
    {
        typeof(InputValidationMiddleware).IsPublic.ShouldBeTrue();
        typeof(InputValidationMiddleware).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InputValidationMiddleware(null!, new InputValidationOptions(), [], _securityEventLogger));
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InputValidationMiddleware(_logger, null!, [], _securityEventLogger));
    }

    [Fact]
    public void ThrowWhenValidatorsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InputValidationMiddleware(_logger, new InputValidationOptions(), null!, _securityEventLogger));
    }

    [Fact]
    public void ThrowWhenSecurityEventLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new InputValidationMiddleware(_logger, new InputValidationOptions(), [], null!));
    }

    [Fact]
    public async Task SkipValidationWhenDisabled()
    {
        // Arrange
        var options = new InputValidationOptions { EnableValidation = false };
        var sut = new InputValidationMiddleware(_logger, options, [], _securityEventLogger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

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

    [Fact]
    public async Task RunCustomValidators()
    {
        // Arrange
        var validator = A.Fake<IInputValidator>();
        A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
            .Returns(Task.FromResult(InputValidationResult.Success()));

        var options = new InputValidationOptions
        {
            EnableValidation = true,
            AllowNullProperties = true,
            AllowEmptyStrings = true,
            BlockSqlInjection = false,
            BlockNoSqlInjection = false,
            BlockCommandInjection = false,
            BlockPathTraversal = false,
            BlockLdapInjection = false,
            BlockHtmlContent = false,
            BlockControlCharacters = false,
            RequireCorrelationId = false,
            MaxMessageSizeBytes = int.MaxValue,
            MaxObjectDepth = 100,
        };
        var sut = new InputValidationMiddleware(_logger, options, [validator], _securityEventLogger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        A.CallTo(() => validator.ValidateAsync(_message, _context))
            .MustHaveHappenedOnceExactly();
    }

    private InputValidationMiddleware CreateMiddleware()
    {
        return new InputValidationMiddleware(
            _logger,
            new InputValidationOptions { EnableValidation = true },
            [],
            _securityEventLogger);
    }
}
