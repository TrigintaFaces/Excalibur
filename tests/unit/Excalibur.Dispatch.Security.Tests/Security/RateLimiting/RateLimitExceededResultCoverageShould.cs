// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitExceededResultCoverageShould
{
    [Fact]
    public void ImplementIMessageResult()
    {
        // Arrange & Act
        var result = new RateLimitExceededResult();

        // Assert
        result.ShouldBeAssignableTo<IMessageResult>();
    }

    [Fact]
    public void SetAllProperties()
    {
        // Arrange
        var problemDetails = A.Fake<IMessageProblemDetails>();
        var routingDecision = new RoutingDecision { Transport = "local", Endpoints = [] };

        // Act
        var result = new RateLimitExceededResult
        {
            Succeeded = false,
            ProblemDetails = problemDetails,
            RoutingDecision = routingDecision,
            ValidationResult = "validation",
            AuthorizationResult = "authorization",
            ErrorMessage = "Rate limit exceeded",
            CacheHit = true,
            RetryAfterMilliseconds = 3000,
            RateLimitKey = "tenant:abc",
        };

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldBe(problemDetails);
        result.RoutingDecision.ShouldBe(routingDecision);
        result.ValidationResult.ShouldBe("validation");
        result.AuthorizationResult.ShouldBe("authorization");
        result.ErrorMessage.ShouldBe("Rate limit exceeded");
        result.CacheHit.ShouldBeTrue();
        result.RetryAfterMilliseconds.ShouldBe(3000);
        result.RateLimitKey.ShouldBe("tenant:abc");
    }

    [Fact]
    public void HaveNullDefaultsForOptionalProperties()
    {
        // Act
        var result = new RateLimitExceededResult();

        // Assert
        result.ProblemDetails.ShouldBeNull();
        result.RoutingDecision.ShouldBeNull();
        result.ValidationResult.ShouldBeNull();
        result.AuthorizationResult.ShouldBeNull();
        result.ErrorMessage.ShouldBeNull();
        result.RateLimitKey.ShouldBeNull();
        result.CacheHit.ShouldBeFalse();
        result.Succeeded.ShouldBeFalse();
        result.RetryAfterMilliseconds.ShouldBe(0);
    }
}
