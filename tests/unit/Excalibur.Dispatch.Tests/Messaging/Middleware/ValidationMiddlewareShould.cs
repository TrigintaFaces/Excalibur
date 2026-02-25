// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class ValidationMiddlewareShould
{
    private readonly IValidationService _validationService = A.Fake<IValidationService>();
    private readonly ILogger<ValidationMiddleware> _logger;

    public ValidationMiddlewareShould()
    {
        _logger = A.Fake<ILogger<ValidationMiddleware>>();
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
        A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
    }

    private ValidationMiddleware CreateSut(ValidationOptions? options = null)
    {
        var opts = options ?? new ValidationOptions { Enabled = true, UseCustomValidation = true };
        return new ValidationMiddleware(Microsoft.Extensions.Options.Options.Create(opts), _validationService, _logger);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var sut = CreateSut(new ValidationOptions { Enabled = false });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var expectedResult = MessageResult.Success();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(expectedResult),
            CancellationToken.None);

        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task ContinuePipelineWhenValidationSucceeds()
    {
        A.CallTo(() => _validationService.ValidateAsync(
                A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(new MessageValidationResult(true, []));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var expectedResult = MessageResult.Success();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(expectedResult),
            CancellationToken.None);

        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task ThrowValidationExceptionWhenValidationFails()
    {
        var errors = new List<ValidationError>
        {
            new("Name", "Name is required"),
            new("Email", "Email is invalid")
        };

        A.CallTo(() => _validationService.ValidateAsync(
                A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(new MessageValidationResult(false, errors));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<ValidationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task StopOnFirstErrorWhenConfigured()
    {
        var errors = new List<ValidationError>
        {
            new("Name", "Name is required"),
            new("Email", "Email is invalid")
        };

        A.CallTo(() => _validationService.ValidateAsync(
                A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(new MessageValidationResult(false, errors));

        var sut = CreateSut(new ValidationOptions
        {
            Enabled = true,
            UseCustomValidation = true,
            StopOnFirstError = true
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var ex = await Should.ThrowAsync<ValidationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());

        // With StopOnFirstError, only first error should be included
        ex.Message.ShouldContain("Name is required");
    }

    [Fact]
    public void HaveValidationStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
    }

    [Fact]
    public void ApplyToActionsOnly()
    {
        var sut = CreateSut();
        sut.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        var sut = CreateSut();
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.InvokeAsync(
                null!, new MessageContext(),
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task UseDataAnnotationsWhenEnabled()
    {
        A.CallTo(() => _validationService.ValidateAsync(
                A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(new MessageValidationResult(true, []));

        var sut = CreateSut(new ValidationOptions
        {
            Enabled = true,
            UseDataAnnotations = true,
            UseCustomValidation = false
        });

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        // Should not throw for a fake message that passes data annotations
        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task PropagateNonValidationExceptions()
    {
        A.CallTo(() => _validationService.ValidateAsync(
                A<IDispatchMessage>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Validation service error"));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }
}
