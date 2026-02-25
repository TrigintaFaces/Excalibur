// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="JwtAuthenticationMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Authentication")]
public sealed class JwtAuthenticationMiddlewareShould
{
    private const string TestSigningKey = "ThisIsAVeryLongSigningKeyForHmacSha256ThatExceedsMinimumLength!";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";

    private readonly ILogger<JwtAuthenticationMiddleware> _logger;
    private readonly ITelemetrySanitizer _sanitizer;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;
    private readonly IMessageResult _successResult;
    private readonly Dictionary<string, object> _contextItems;

    public JwtAuthenticationMiddlewareShould()
    {
        _logger = new NullLogger<JwtAuthenticationMiddleware>();
        _sanitizer = A.Fake<ITelemetrySanitizer>();
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
        // Arrange
        var sut = CreateMiddleware();

        // Assert
        sut.ShouldBeAssignableTo<IDispatchMiddleware>();
    }

    [Fact]
    public void HaveAuthenticationStage()
    {
        // Arrange
        var sut = CreateMiddleware();

        // Assert
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Authentication);
    }

    [Fact]
    public void BePublicAndSealed()
    {
        // Assert
        typeof(JwtAuthenticationMiddleware).IsPublic.ShouldBeTrue();
        typeof(JwtAuthenticationMiddleware).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JwtAuthenticationMiddleware(null!, _sanitizer, _logger));
    }

    [Fact]
    public void ThrowWhenSanitizerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JwtAuthenticationMiddleware(CreateOptions(), null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new JwtAuthenticationMiddleware(CreateOptions(), _sanitizer, null!));
    }

    [Fact]
    public async Task SkipAuthenticationWhenDisabled()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        A.CallTo(() => _nextDelegate(_message, _context, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SkipAuthenticationForAnonymousMessageTypes()
    {
        // Arrange
        var options = CreateOptions(anonymousTypes: new HashSet<string>(StringComparer.Ordinal) { _message.GetType().Name });
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnFailedResultWhenTokenMissingAndRequired()
    {
        // Arrange — Items has no "Authorization" key
        var options = CreateOptions(requireAuthentication: true);
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.ShouldBeAssignableTo<AuthenticationFailedResult>();
        result.Succeeded.ShouldBeFalse();
        ((AuthenticationFailedResult)result).Reason.ShouldBe(AuthenticationFailureReason.MissingToken);
    }

    [Fact]
    public async Task ContinueWhenTokenMissingAndNotRequired()
    {
        // Arrange — Items has no "Authorization" key
        var options = CreateOptions(requireAuthentication: false);
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        // Arrange
        var sut = CreateMiddleware();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(null!, _context, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenContextIsNull()
    {
        // Arrange
        var sut = CreateMiddleware();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, null!, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenNextDelegateIsNull()
    {
        // Arrange
        var sut = CreateMiddleware();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task AuthenticateValidTokenFromContext()
    {
        // Arrange
        var token = GenerateValidJwtToken();
        var options = CreateOptions();
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // Place raw token in Items dictionary using the default TokenContextKey "AuthToken"
        _contextItems["AuthToken"] = token;

        // Also wire up Properties for SetProperty calls
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        A.CallTo(() => _context.Properties).Returns(properties);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        properties.ShouldContainKey("Principal");
    }

    [Fact]
    public async Task ExtractTokenFromMessageHeaders()
    {
        // Arrange
        var token = GenerateValidJwtToken();
        var options = CreateOptions();
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        // No token in Items
        // Token in message headers — fake must implement both IDispatchMessage and IMessageWithHeaders
        var msgWithHeaders = A.Fake<IDispatchMessage>(o => o.Implements<IMessageWithHeaders>());
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = $"Bearer {token}" };
        A.CallTo(() => ((IMessageWithHeaders)msgWithHeaders).Headers).Returns(headers);

        A.CallTo(() => _nextDelegate(msgWithHeaders, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        // Wire up Properties for SetProperty calls
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        A.CallTo(() => _context.Properties).Returns(properties);

        // Act
        var result = await sut.InvokeAsync(msgWithHeaders, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnInvalidTokenResultForMalformedToken()
    {
        // Arrange
        var options = CreateOptions();
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        _contextItems["AuthToken"] = "not-a-valid-jwt-token";

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnExpiredTokenResultForExpiredToken()
    {
        // Arrange
        var token = GenerateExpiredJwtToken();
        var options = CreateOptions();
        var sut = new JwtAuthenticationMiddleware(options, _sanitizer, _logger);

        _contextItems["AuthToken"] = token;

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ShouldBeAssignableTo<AuthenticationFailedResult>();
        ((AuthenticationFailedResult)result).Reason.ShouldBe(AuthenticationFailureReason.TokenExpired);
    }

    [Fact]
    public void HaveApplicableMessageKindsForActionAndEvent()
    {
        // Arrange
        var sut = CreateMiddleware();

        // Assert
        sut.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
    }

    private JwtAuthenticationMiddleware CreateMiddleware()
    {
        return new JwtAuthenticationMiddleware(CreateOptions(), _sanitizer, _logger);
    }

    private static IOptions<JwtAuthenticationOptions> CreateOptions(
        bool enabled = true,
        bool requireAuthentication = true,
        ISet<string>? anonymousTypes = null)
    {
        var opts = new JwtAuthenticationOptions
        {
            Enabled = enabled,
            RequireAuthentication = requireAuthentication,
        };

        if (anonymousTypes != null)
        {
            foreach (var t in anonymousTypes)
            {
                opts.AllowAnonymousMessageTypes.Add(t);
            }
        }

        opts.Credentials.SigningKey = TestSigningKey;
        opts.Credentials.ValidIssuer = TestIssuer;
        opts.Credentials.ValidAudience = TestAudience;

        return Microsoft.Extensions.Options.Options.Create(opts);
    }

    private static string GenerateValidJwtToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateExpiredJwtToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
