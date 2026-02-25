// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
public sealed class ExceptionMapperShould
{
    private static ExceptionMapper CreateSut(
        IReadOnlyList<ExceptionMapping>? mappings = null,
        Func<Exception, IMessageProblemDetails>? defaultMapper = null,
        bool useApiExceptionMapping = true)
    {
        var options = new ExceptionMapperOptions(
            mappings ?? [],
            defaultMapper ?? (ex => new MessageProblemDetails
            {
                Type = "InternalError",
                Title = "Internal Error",
                ErrorCode = 500,
                Status = 500,
                Detail = ex.Message
            }),
            useApiExceptionMapping);
        return new ExceptionMapper(options);
    }

    [Fact]
    public void MapUsingDefaultMapperWhenNoMappingsMatch()
    {
        var sut = CreateSut();

        var result = sut.Map(new InvalidOperationException("test error"));

        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe(500);
    }

    [Fact]
    public void ThrowOnNullException()
    {
        var sut = CreateSut();

        Should.Throw<ArgumentNullException>(() => sut.Map(null!));
    }

    [Fact]
    public void CanMapReturnsTrueWithDefaultMapper()
    {
        var sut = CreateSut();

        sut.CanMap(new InvalidOperationException("test")).ShouldBeTrue();
    }

    [Fact]
    public void ThrowOnNullExceptionForCanMap()
    {
        var sut = CreateSut();

        Should.Throw<ArgumentNullException>(() => sut.CanMap(null!));
    }

    [Fact]
    public async Task MapAsyncUsingDefaultMapper()
    {
        var sut = CreateSut();

        var result = await sut.MapAsync(new InvalidOperationException("test"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe(500);
    }

    [Fact]
    public async Task MapAsyncThrowsOnNullException()
    {
        var sut = CreateSut();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.MapAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new ExceptionMapper(null!));
    }
}
