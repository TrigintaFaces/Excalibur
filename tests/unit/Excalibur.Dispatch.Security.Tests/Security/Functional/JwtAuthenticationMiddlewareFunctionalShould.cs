// Functional tests for JwtAuthenticationMiddleware — token validation, claims extraction, expiry, skip logic

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security;

using Microsoft.IdentityModel.Tokens;

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class JwtAuthenticationMiddlewareFunctionalShould
{
    private const string TestSigningKey = "ThisIsASecretKeyForTestingPurposesOnly1234567890!";

    private static JwtAuthenticationMiddleware CreateMiddleware(
        JwtAuthenticationOptions? opts = null,
        ICredentialStore? credentialStore = null)
    {
        opts ??= new JwtAuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateSigningKey = true,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            SigningKey = TestSigningKey,
        };

        var sanitizer = A.Fake<ITelemetrySanitizer>();
        A.CallTo(() => sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => (string?)call.Arguments[1]);

        return new JwtAuthenticationMiddleware(
            Microsoft.Extensions.Options.Options.Create(opts),
            sanitizer,
            NullLogger<JwtAuthenticationMiddleware>.Instance,
            credentialStore);
    }

    private static string CreateValidJwt(
        string issuer = "test-issuer",
        string audience = "test-audience",
        string? userId = "user-123",
        string? name = null,
        string? email = null,
        string? tenantId = null,
        string[]? roles = null,
        TimeSpan? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>();
        if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (name != null) claims.Add(new Claim(ClaimTypes.Name, name));
        if (email != null) claims.Add(new Claim(ClaimTypes.Email, email));
        if (tenantId != null) claims.Add(new Claim("tenant_id", tenantId));
        if (roles != null)
        {
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expires ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (IDispatchMessage Message, IMessageContext Context, DispatchRequestDelegate Next, IMessageResult SuccessResult) CreatePipelineFakes(
        string? tokenInItems = null)
    {
        var message = A.Fake<IDispatchMessage>();

        // Use real dictionaries so extension methods work
        var items = new Dictionary<string, object>();
        if (tokenInItems != null)
        {
            items["AuthToken"] = tokenInItems;
        }

        var properties = new Dictionary<string, object?>();

        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(properties);

        var successResult = A.Fake<IMessageResult>();
        A.CallTo(() => successResult.Succeeded).Returns(true);

        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(successResult);

        return (message, context, next, successResult);
    }

    [Fact]
    public async Task SkipAuthenticationWhenDisabled()
    {
        var middleware = CreateMiddleware(new JwtAuthenticationOptions { Enabled = false });
        var (message, context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.ShouldBe(successResult);
    }

    [Fact]
    public async Task SkipAuthenticationForAnonymousMessageTypes()
    {
        var opts = new JwtAuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateSigningKey = true,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            SigningKey = TestSigningKey,
            AllowAnonymousMessageTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(TestAnonymousMessage),
            },
        };

        var middleware = CreateMiddleware(opts);

        var (_, context, next, successResult) = CreatePipelineFakes();

        // Use a real message instance to get correct GetType()
        var realMessage = new TestAnonymousMessage();
        var result = await middleware.InvokeAsync(realMessage, context, next, CancellationToken.None);

        result.ShouldBe(successResult);
    }

    [Fact]
    public async Task ReturnFailureWhenTokenMissingAndRequired()
    {
        var middleware = CreateMiddleware();
        // No token in items
        var (message, context, next, _) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ShouldBeOfType<AuthenticationFailedResult>()
            .Reason.ShouldBe(AuthenticationFailureReason.MissingToken);
    }

    [Fact]
    public async Task ContinueWithoutAuthWhenTokenMissingButNotRequired()
    {
        var opts = new JwtAuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = false,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            SigningKey = TestSigningKey,
        };
        var middleware = CreateMiddleware(opts);
        var (message, context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.ShouldBe(successResult);
    }

    [Fact]
    public async Task AuthenticateWithValidTokenFromContext()
    {
        var token = CreateValidJwt(userId: "user-42", name: "Alice", email: "alice@test.com");

        var middleware = CreateMiddleware();
        var (message, context, next, successResult) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();

        // Verify claims were written to Properties via the extension method
        var properties = context.Properties;
        properties.ShouldNotBeNull();
        properties["UserId"].ShouldBe("user-42");
        properties["UserName"].ShouldBe("Alice");
        properties["Email"].ShouldBe("alice@test.com");
    }

    [Fact]
    public async Task ExtractTenantIdFromToken()
    {
        var token = CreateValidJwt(tenantId: "tenant-abc");

        var middleware = CreateMiddleware();
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        context.Properties["TenantId"].ShouldBe("tenant-abc");
    }

    [Fact]
    public async Task ExtractRolesFromToken()
    {
        var token = CreateValidJwt(roles: ["Admin", "User"]);

        var middleware = CreateMiddleware();
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        var roles = context.Properties["Roles"].ShouldBeOfType<List<string>>();
        roles.ShouldContain("Admin");
        roles.ShouldContain("User");
    }

    [Fact]
    public async Task ReturnExpiredTokenFailure()
    {
        // Create a token that is already expired
        var token = CreateValidJwt(expires: TimeSpan.FromSeconds(-60));

        var opts = new JwtAuthenticationOptions
        {
            Enabled = true,
            RequireAuthentication = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateSigningKey = true,
            ValidIssuer = "test-issuer",
            ValidAudience = "test-audience",
            SigningKey = TestSigningKey,
            ClockSkewSeconds = 0,
        };

        var middleware = CreateMiddleware(opts);
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ShouldBeOfType<AuthenticationFailedResult>()
            .Reason.ShouldBe(AuthenticationFailureReason.TokenExpired);
    }

    [Fact]
    public async Task ReturnInvalidTokenFailureForWrongSigningKey()
    {
        // Create token with different key
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("DifferentKeyThatDoesNotMatchTheExpected1234567890!"));
        var credentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var middleware = CreateMiddleware();
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: tokenString);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        var authResult = result.ShouldBeOfType<AuthenticationFailedResult>();
        authResult.Reason.ShouldBe(AuthenticationFailureReason.ValidationError);
    }

    [Fact]
    public async Task ExtractTokenFromBearerHeader()
    {
        var token = CreateValidJwt();

        var middleware = CreateMiddleware();

        // Create a message that also implements IMessageWithHeaders
        var message = new TestMessageWithHeaders(new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
        });

        var items = new Dictionary<string, object>(); // No token in items
        var properties = new Dictionary<string, object?>();
        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(properties);

        var successResult = A.Fake<IMessageResult>();
        A.CallTo(() => successResult.Succeeded).Returns(true);
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(successResult);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SetStageToAuthentication()
    {
        var middleware = CreateMiddleware();
        middleware.Stage.ShouldBe(DispatchMiddlewareStage.Authentication);
    }

    [Fact]
    public void ApplyToActionsAndEvents()
    {
        var middleware = CreateMiddleware();
        middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
    }

    [Fact]
    public async Task ThrowOnNullMessage()
    {
        var middleware = CreateMiddleware();
        var (_, context, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullContext()
    {
        var middleware = CreateMiddleware();
        var (message, _, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullNextDelegate()
    {
        var middleware = CreateMiddleware();
        var (message, context, _, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ReturnInvalidTokenForMalformedJwt()
    {
        var middleware = CreateMiddleware();
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: "not-a-jwt-token");

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ShouldBeOfType<AuthenticationFailedResult>();
    }

    [Fact]
    public async Task SetAuthenticatedAtTimestamp()
    {
        var token = CreateValidJwt();
        var middleware = CreateMiddleware();
        var before = DateTimeOffset.UtcNow;
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        var authenticatedAt = context.Properties["AuthenticatedAt"].ShouldBeOfType<DateTimeOffset>();
        authenticatedAt.ShouldBeGreaterThanOrEqualTo(before);
        authenticatedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SetDefaultAuthenticationMethodToJwt()
    {
        var token = CreateValidJwt();
        var middleware = CreateMiddleware();
        var (message, context, next, _) = CreatePipelineFakes(tokenInItems: token);

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        context.Properties["AuthenticationMethod"].ShouldBe("jwt");
    }

    // Test message type for anonymous authentication bypass
    private sealed class TestAnonymousMessage : IDispatchMessage { }

    // Test message that implements both IDispatchMessage and IMessageWithHeaders
    private sealed class TestMessageWithHeaders(IDictionary<string, string> headers) : IDispatchMessage, IMessageWithHeaders
    {
        public IDictionary<string, string> Headers { get; } = headers;
    }
}
