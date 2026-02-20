// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security;

using Microsoft.IdentityModel.Tokens;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Deep coverage tests for <see cref="JwtAuthenticationMiddleware"/> covering claims extraction,
/// role extraction, tenant and email claims, token header extraction without Bearer prefix,
/// property extraction path, and credential store interaction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class JwtAuthenticationMiddlewareDepthShould
{
	private const string TestSigningKey = "ThisIsAVeryLongSigningKeyForHmacSha256ThatExceedsMinimumLengthRequirement!";
	private const string TestIssuer = "depth-test-issuer";
	private const string TestAudience = "depth-test-audience";

	private readonly ITelemetrySanitizer _sanitizer;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;
	private readonly IMessageResult _successResult;
	private readonly Dictionary<string, object> _contextItems;
	private readonly Dictionary<string, object?> _contextProperties;

	public JwtAuthenticationMiddlewareDepthShould()
	{
		_sanitizer = A.Fake<ITelemetrySanitizer>();
		// Pass-through sanitizer
		A.CallTo(() => _sanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily((string _, string? value) => value);

		_message = A.Fake<IDispatchMessage>();
		_context = A.Fake<IMessageContext>();
		_successResult = A.Fake<IMessageResult>();
		_contextItems = new Dictionary<string, object>(StringComparer.Ordinal);
		_contextProperties = new Dictionary<string, object?>(StringComparer.Ordinal);

		A.CallTo(() => _successResult.Succeeded).Returns(true);
		A.CallTo(() => _context.Items).Returns(_contextItems);
		A.CallTo(() => _context.Properties).Returns(_contextProperties);
	}

	[Fact]
	public async Task ExtractClaims_SetUserIdFromSubClaim()
	{
		// Arrange
		var token = GenerateToken(claims: [new Claim("sub", "user-from-sub")]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("UserId");
		_contextProperties["UserId"].ShouldBe("user-from-sub");
	}

	[Fact]
	public async Task ExtractClaims_SetEmailFromEmailClaim()
	{
		// Arrange
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim("email", "user@test.com"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("Email");
		_contextProperties["Email"].ShouldBe("user@test.com");
	}

	[Fact]
	public async Task ExtractClaims_TidClaimMappedByHandler_DoesNotSetTenantId()
	{
		// Arrange — The default JwtSecurityTokenHandler maps "tid" to a long URI via
		// DefaultInboundClaimTypeMap, so FindFirst("tid") returns null. This documents
		// that "tenant_id" is the reliable claim name for tenant extraction.
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim("tid", "tenant-from-tid"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — TenantId NOT set because "tid" is remapped by the default JWT handler
		_contextProperties.ShouldNotContainKey("TenantId");
	}

	[Fact]
	public async Task ExtractClaims_SetTenantIdFromTenantIdClaim()
	{
		// Arrange — "tenant_id" is NOT in the default inbound claim type map,
		// so it passes through and FindFirst("tenant_id") finds it correctly.
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim("tenant_id", "tenant-abc"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("TenantId");
		_contextProperties["TenantId"].ShouldBe("tenant-abc");
	}

	[Fact]
	public async Task ExtractClaims_SetRoles()
	{
		// Arrange
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim(ClaimTypes.Role, "Admin"),
			new Claim("role", "Editor"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("Roles");
		var roles = _contextProperties["Roles"] as List<string>;
		roles.ShouldNotBeNull();
		roles.ShouldContain("Admin");
		roles.ShouldContain("Editor");
	}

	[Fact]
	public async Task ExtractClaims_SetAuthenticatedAtAndMethod()
	{
		// Arrange
		var token = GenerateToken(claims: [new Claim(ClaimTypes.NameIdentifier, "user-1")]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);
		var before = DateTimeOffset.UtcNow;

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("AuthenticatedAt");
		var authTime = (DateTimeOffset)_contextProperties["AuthenticatedAt"]!;
		authTime.ShouldBeGreaterThanOrEqualTo(before);

		_contextProperties.ShouldContainKey("AuthenticationMethod");
		_contextProperties["AuthenticationMethod"].ShouldBe("jwt"); // default when no amr claim
	}

	[Fact]
	public async Task ExtractClaims_AmrClaimMappedByHandler_DefaultsToJwt()
	{
		// Arrange — The default JwtSecurityTokenHandler maps "amr" to
		// "http://schemas.microsoft.com/claims/authnmethodsreference" via
		// DefaultInboundClaimTypeMap, so FindFirst("amr") returns null.
		// The middleware falls back to "jwt" as the default authentication method.
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim("amr", "mfa"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — AuthenticationMethod defaults to "jwt" because "amr" is remapped
		_contextProperties.ShouldContainKey("AuthenticationMethod");
		_contextProperties["AuthenticationMethod"].ShouldBe("jwt");
	}

	[Fact]
	public async Task ExtractToken_FromHeaderWithoutBearerPrefix()
	{
		// Arrange — message with headers, token WITHOUT "Bearer " prefix
		var token = GenerateToken(claims: [new Claim(ClaimTypes.NameIdentifier, "user-header")]);
		var msgWithHeaders = A.Fake<IDispatchMessage>(o => o.Implements<IMessageWithHeaders>());
		var headers = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["Authorization"] = token, // No "Bearer " prefix
		};
		A.CallTo(() => ((IMessageWithHeaders)msgWithHeaders).Headers).Returns(headers);

		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(msgWithHeaders, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_contextProperties.ShouldContainKey("UserId");
		_contextProperties["UserId"].ShouldBe("user-header");
	}

	[Fact]
	public async Task ReturnValidationError_ForTokenWithWrongSigningKey()
	{
		// Arrange — token signed with a different key
		var wrongKey = new SymmetricSecurityKey(
			Encoding.UTF8.GetBytes("ThisIsACompletelyDifferentKeyThatShouldNotMatchTheConfiguredKey!"));
		var credentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);
		var jwtToken = new JwtSecurityToken(
			issuer: TestIssuer,
			audience: TestAudience,
			claims: [new Claim(ClaimTypes.NameIdentifier, "hacker")],
			expires: DateTime.UtcNow.AddHours(1),
			signingCredentials: credentials);
		var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ShouldBeAssignableTo<AuthenticationFailedResult>();
		var failedResult = (AuthenticationFailedResult)result;
		failedResult.Reason.ShouldBe(AuthenticationFailureReason.ValidationError);
	}

	[Fact]
	public async Task ReturnValidationError_ForTokenWithWrongIssuer()
	{
		// Arrange — token with wrong issuer
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var jwtToken = new JwtSecurityToken(
			issuer: "wrong-issuer",
			audience: TestAudience,
			claims: [new Claim(ClaimTypes.NameIdentifier, "user-1")],
			expires: DateTime.UtcNow.AddHours(1),
			signingCredentials: credentials);
		var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnValidationError_ForTokenWithWrongAudience()
	{
		// Arrange — token with wrong audience
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var jwtToken = new JwtSecurityToken(
			issuer: TestIssuer,
			audience: "wrong-audience",
			claims: [new Claim(ClaimTypes.NameIdentifier, "user-1")],
			expires: DateTime.UtcNow.AddHours(1),
			signingCredentials: credentials);
		var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task SetPrincipalInContext_OnSuccessfulAuth()
	{
		// Arrange
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "principal-user"),
			new Claim(ClaimTypes.Name, "Principal User"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("Principal");
		var principal = _contextProperties["Principal"] as ClaimsPrincipal;
		principal.ShouldNotBeNull();
		principal.Identity.ShouldNotBeNull();
		principal.Identity!.IsAuthenticated.ShouldBeTrue();
	}

	[Fact]
	public async Task SetUserName_FromNameClaim()
	{
		// Arrange
		var token = GenerateToken(claims:
		[
			new Claim(ClaimTypes.NameIdentifier, "user-1"),
			new Claim("name", "John Doe"),
		]);
		_contextItems["AuthToken"] = token;
		var sut = CreateMiddleware();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		_contextProperties.ShouldContainKey("UserName");
		_contextProperties["UserName"].ShouldBe("John Doe");
	}

	[Fact]
	public async Task HandleAsyncKeyRetrieval_WithCredentialStore()
	{
		// Arrange — set up credential store that returns a signing key
		var credentialStore = A.Fake<ICredentialStore>();
		var secureKey = new System.Security.SecureString();
		foreach (var c in TestSigningKey)
		{
			secureKey.AppendChar(c);
		}

		secureKey.MakeReadOnly();

		A.CallTo(() => credentialStore.GetCredentialAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<System.Security.SecureString?>(secureKey));

		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			RequireAuthentication = true,
		};
		options.Credentials.SigningKey = TestSigningKey;
		options.Credentials.ValidIssuer = TestIssuer;
		options.Credentials.ValidAudience = TestAudience;
		options.Credentials.UseAsyncKeyRetrieval = true;
		options.Credentials.SigningKeyCredentialName = "jwt-signing-key";

		var sut = new JwtAuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(options),
			_sanitizer,
			NullLogger<JwtAuthenticationMiddleware>.Instance,
			credentialStore);

		var token = GenerateToken(claims: [new Claim(ClaimTypes.NameIdentifier, "async-user")]);
		_contextItems["AuthToken"] = token;

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleAsyncKeyRetrieval_NoCredentialStore_FallbackToStaticKey()
	{
		// Arrange — UseAsyncKeyRetrieval=true but no credential store provided
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			RequireAuthentication = true,
		};
		options.Credentials.SigningKey = TestSigningKey;
		options.Credentials.ValidIssuer = TestIssuer;
		options.Credentials.ValidAudience = TestAudience;
		options.Credentials.UseAsyncKeyRetrieval = true;
		// No credential name set, so it falls back to static key

		var sut = new JwtAuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(options),
			_sanitizer,
			NullLogger<JwtAuthenticationMiddleware>.Instance);

		var token = GenerateToken(claims: [new Claim(ClaimTypes.NameIdentifier, "fallback-user")]);
		_contextItems["AuthToken"] = token;

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(_successResult);

		// Act
		var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

		// Assert — should succeed using static signing key fallback
		result.Succeeded.ShouldBeTrue();
	}

	private JwtAuthenticationMiddleware CreateMiddleware()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			RequireAuthentication = true,
		};
		options.Credentials.SigningKey = TestSigningKey;
		options.Credentials.ValidIssuer = TestIssuer;
		options.Credentials.ValidAudience = TestAudience;

		return new JwtAuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(options),
			_sanitizer,
			NullLogger<JwtAuthenticationMiddleware>.Instance);
	}

	private static string GenerateToken(IEnumerable<Claim> claims)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
		var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: TestIssuer,
			audience: TestAudience,
			claims: claims,
			expires: DateTime.UtcNow.AddHours(1),
			signingCredentials: credentials);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}
}
