// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;

using A3AuthorizationMiddleware = Excalibur.A3.Authorization.AuthorizationMiddleware;

namespace Excalibur.Tests.A3.Authorization.Conditions;

/// <summary>
/// Integration tests for condition expression evaluation through the AuthorizationMiddleware.
/// Exercises the full path: [RequirePermission(When = "...")] -> middleware -> cache -> evaluator.
/// Addresses SoftwareArchitect residual risk #1 from Sprint 727 review.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class ConditionMiddlewareIntegrationShould
{
    private readonly IAccessToken _accessToken;
    private readonly IDispatchAuthorizationService _authorizationService;
    private readonly AttributeAuthorizationCache _attributeCache;
    private readonly A3AuthorizationMiddleware _sut;

    public ConditionMiddlewareIntegrationShould()
    {
        _accessToken = A.Fake<IAccessToken>();
        _authorizationService = A.Fake<IDispatchAuthorizationService>();
        _attributeCache = new AttributeAuthorizationCache();
        _sut = new A3AuthorizationMiddleware(
            _accessToken, _authorizationService, _attributeCache,
            new ConditionExpressionEvaluator());
    }

    private void SetupAuthorized()
    {
        A.CallTo(() => _accessToken.Claims).Returns([]);
        A.CallTo(() => _accessToken.IsAuthenticated()).Returns(true);
        A.CallTo(() => _authorizationService.AuthorizeAsync(
                A<ClaimsPrincipal>.Ignored,
                A<string>.Ignored,
                A<IAuthorizationRequirement[]>.Ignored))
            .Returns(Excalibur.Dispatch.Abstractions.AuthorizationResult.Success());
    }

    // ──────────────────────────────────────────────
    // When condition with claims
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PermitWhenConditionMatchesClaim()
    {
        // Arrange
        SetupAuthorized();
        A.CallTo(() => _accessToken.Claims).Returns(
        [
            new Claim("Role", "admin"),
        ]);

        var message = new AdminOnlyMessage();
        var context = A.Fake<IMessageContext>();
        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        // Act
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert -- condition "subject.Role == 'admin'" should pass
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task DenyWhenConditionDoesNotMatchClaim()
    {
        // Arrange
        SetupAuthorized();
        A.CallTo(() => _accessToken.Claims).Returns(
        [
            new Claim("Role", "user"),
        ]);

        var message = new AdminOnlyMessage();
        var context = A.Fake<IMessageContext>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        // Act
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert -- condition "subject.Role == 'admin'" should fail for "user"
        result.ShouldNotBeNull();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Status.ShouldBe(403);
        result.ProblemDetails.Title.ShouldBe("Condition Not Met");
    }

    [Fact]
    public async Task DenyWhenClaimIsMissing()
    {
        // Arrange
        SetupAuthorized();
        A.CallTo(() => _accessToken.Claims).Returns([]); // No claims at all

        var message = new AdminOnlyMessage();
        var context = A.Fake<IMessageContext>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        // Act
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert -- missing "Role" claim means null, != 'admin' -> deny
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Status.ShouldBe(403);
    }

    // ──────────────────────────────────────────────
    // When condition with action attributes
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PermitWhenActionNameMatchesCondition()
    {
        // Arrange
        SetupAuthorized();

        var message = new ActionNameCheckMessage();
        var context = A.Fake<IMessageContext>();
        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        // Act -- condition is "action.Name == 'orders.read'" and permission is "orders.read"
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    // ──────────────────────────────────────────────
    // When condition with resource attributes
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PermitWhenResourceTypeMatchesCondition()
    {
        // Arrange
        SetupAuthorized();

        var message = new ResourceTypeCheckMessage();
        var context = A.Fake<IMessageContext>();
        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        // Act -- condition checks resource.Type == message class name
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    // ──────────────────────────────────────────────
    // Malformed expression -> deny (fail-closed)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DenyWhenExpressionIsMalformed()
    {
        // Arrange
        SetupAuthorized();

        var message = new MalformedConditionMessage();
        var context = A.Fake<IMessageContext>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        // Act
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert -- malformed expression cached as null -> deny
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Status.ShouldBe(403);
        result.ProblemDetails.Detail.ShouldContain("Malformed");
    }

    // ──────────────────────────────────────────────
    // No When condition -> proceed normally
    // ──────────────────────────────────────────────

    [Fact]
    public async Task ProceedWhenNoConditionSpecified()
    {
        // Arrange
        SetupAuthorized();

        var message = new NoConditionMessage();
        var context = A.Fake<IMessageContext>();
        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        // Act
        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert -- no When condition, should proceed to next
        result.ShouldBe(expectedResult);
    }

    // ──────────────────────────────────────────────
    // Test message types with RequirePermission attributes
    // ──────────────────────────────────────────────

    [RequirePermission("admin.action", When = "subject.Role == 'admin'")]
    private sealed class AdminOnlyMessage : IDispatchMessage
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
    }

    [RequirePermission("orders.read", When = "action.Name == 'orders.read'")]
    private sealed class ActionNameCheckMessage : IDispatchMessage
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
    }

    [RequirePermission("resource.view", When = "resource.Type == 'ResourceTypeCheckMessage'")]
    private sealed class ResourceTypeCheckMessage : IDispatchMessage
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
    }

    [RequirePermission("some.action", When = "this is not valid!!!")]
    private sealed class MalformedConditionMessage : IDispatchMessage
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
    }

    [RequirePermission("basic.action")]
    private sealed class NoConditionMessage : IDispatchMessage
    {
        public Guid MessageId { get; init; } = Guid.NewGuid();
    }
}
