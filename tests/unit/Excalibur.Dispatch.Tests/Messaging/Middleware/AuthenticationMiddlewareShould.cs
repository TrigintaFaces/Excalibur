// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class AuthenticationMiddlewareShould
{
    private readonly IAuthenticationService _authService = A.Fake<IAuthenticationService>();
    private readonly ITelemetrySanitizer _sanitizer = A.Fake<ITelemetrySanitizer>();
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddlewareShould()
    {
        _logger = A.Fake<ILogger<AuthenticationMiddleware>>();
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
        A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
        A.CallTo(() => _sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => call.GetArgument<string?>(1));
    }

    private AuthenticationMiddleware CreateSut(AuthenticationOptions? options = null)
    {
        var opts = options ?? new AuthenticationOptions { Enabled = true, RequireAuthentication = true };
        return new AuthenticationMiddleware(Microsoft.Extensions.Options.Options.Create(opts), _authService, _sanitizer, _logger);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var sut = CreateSut(new AuthenticationOptions { Enabled = false });
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
    public async Task ThrowUnauthorizedWhenNoTokenAndAuthRequired()
    {
        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task AllowAnonymousWhenNotRequired()
    {
        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthenticateWithBearerToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "testuser")], "Bearer"));

        A.CallTo(() => _authService.AuthenticateBearerTokenAsync(
                A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(principal));

        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            EnableCaching = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        context.SetItem("Authorization", "Bearer my-jwt-token");

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        context.GetItem<ClaimsPrincipal>("Principal").ShouldNotBeNull();
        context.GetItem<string>("UserId").ShouldBe("testuser");
    }

    [Fact]
    public async Task ThrowWhenAuthenticationFails()
    {
        A.CallTo(() => _authService.AuthenticateBearerTokenAsync(
                A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(null));

        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            EnableCaching = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        context.SetItem("Authorization", "Bearer invalid-token");

        await Should.ThrowAsync<UnauthorizedAccessException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public void HaveAuthenticationStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Authentication);
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
    public async Task AuthenticateWithApiKey()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "api-user")], "ApiKey"));

        A.CallTo(() => _authService.AuthenticateApiKeyAsync(
                A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<ClaimsPrincipal?>(principal));

        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            EnableCaching = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        context.SetItem("ApiKey", CreateNonSecretApiKeyValue());

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        context.GetItem<string>("UserId").ShouldBe("api-user");
    }

    [Fact]
    public async Task PropagateUnexpectedExceptions()
    {
        A.CallTo(() => _authService.AuthenticateBearerTokenAsync(
                A<string>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("auth service broken"));

        var sut = CreateSut(new AuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            EnableCaching = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        context.SetItem("Authorization", "Bearer some-token");

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    private static string CreateNonSecretApiKeyValue()
    {
        return string.Concat("fixture-", "auth-", "key");
    }
}
