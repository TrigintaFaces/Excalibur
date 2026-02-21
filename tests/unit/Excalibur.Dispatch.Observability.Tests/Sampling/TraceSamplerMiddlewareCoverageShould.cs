// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Sampling;

#pragma warning disable CA2012

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TraceSamplerMiddlewareCoverageShould
{
    private readonly ITraceSampler _sampler;
    private readonly TraceSamplerMiddleware _sut;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly Dictionary<string, object> _contextItems;

    public TraceSamplerMiddlewareCoverageShould()
    {
        _sampler = A.Fake<ITraceSampler>();
        _sut = new TraceSamplerMiddleware(_sampler);
        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        _contextItems = new Dictionary<string, object>();
        A.CallTo(() => _context.Items).Returns(_contextItems);
    }

    [Fact]
    public void HaveStartStage()
    {
        _sut.Stage.ShouldBe(DispatchMiddlewareStage.Start);
    }

    [Fact]
    public async Task CallNextDelegateWhenSampled()
    {
        // Arrange
        A.CallTo(() => _sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(true);

        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

        // Act
        var result = await _sut.InvokeAsync(_message, _context, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task SetSampledFalseWhenNotSampled()
    {
        // Arrange
        A.CallTo(() => _sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(false);
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

        // Act
        await _sut.InvokeAsync(_message, _context, next, CancellationToken.None);

        // Assert
        A.CallTo(() => _context.SetItem("dispatch.trace.sampled", false)).MustHaveHappened();
    }

    [Fact]
    public async Task NotSetSampledFlagWhenSampled()
    {
        // Arrange
        A.CallTo(() => _sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(true);
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

        // Act
        await _sut.InvokeAsync(_message, _context, next, CancellationToken.None);

        // Assert
        A.CallTo(() => _context.SetItem("dispatch.trace.sampled", A<object>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullMessage()
    {
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(null!, _context, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContext()
    {
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(_message, null!, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullNextDelegate()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullSampler()
    {
        Should.Throw<ArgumentNullException>(() => new TraceSamplerMiddleware(null!));
    }
}
