// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class ExceptionMappingMiddlewareShould
{
    private readonly IExceptionMapper _mapper = A.Fake<IExceptionMapper>();

    private ExceptionMappingMiddleware CreateSut() =>
        new(_mapper, NullLogger<ExceptionMappingMiddleware>.Instance);

    [Fact]
    public async Task PassThroughWhenNoException()
    {
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
    public async Task MapExceptionToProblemDetails()
    {
        var problemDetails = new MessageProblemDetails
        {
            Type = "NotFound",
            Title = "Resource Not Found",
            ErrorCode = 404,
            Status = 404,
            Detail = "The requested resource was not found"
        };

        A.CallTo(() => _mapper.MapAsync(A<Exception>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IMessageProblemDetails>(problemDetails));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new KeyNotFoundException("Not found"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.ErrorCode.ShouldBe(404);
    }

    [Fact]
    public async Task PropagateOperationCanceledException()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new OperationCanceledException(),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ReturnFallbackProblemDetailsWhenMappingFails()
    {
        A.CallTo(() => _mapper.MapAsync(A<Exception>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Mapper broke"));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new InvalidOperationException("Original error"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.ErrorCode.ShouldBe(500);
        result.ProblemDetails.Title.ShouldBe("Exception Mapping Failed");
    }

    [Fact]
    public void HaveErrorHandlingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
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
    public async Task ThrowWhenContextIsNull()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.InvokeAsync(
                message, null!,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ThrowWhenNextDelegateIsNull()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.InvokeAsync(message, new MessageContext(), null!, CancellationToken.None).AsTask());
    }
}
