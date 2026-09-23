// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Locks the disclosure contract for authorization denials: a 401 or 403 response body carries NO authorization
/// vocabulary — no policy name, no role name, no evaluation failure message — and the specific reason is logged
/// server-side instead.
/// </summary>
/// <remarks>
/// <para>
/// Each behaviour is locked by two arms, because either alone passes for the wrong reason:
/// </para>
/// <list type="bullet">
/// <item>
/// SAFETY — the distinctive test-chosen name is literally absent from the serialized body. Asserted against the
/// exact string, not a regex over generic words, so a body that merely reworded the leak still fails.
/// </item>
/// <item>
/// LIVENESS — the request is still REFUSED and the specific name IS present in captured log output. Without this,
/// a middleware that stopped denying, or one that logged nothing, would pass the safety arm trivially.
/// </item>
/// </list>
/// <para>
/// The third arm calls <see cref="DispatchProblemDetails.ForForbidden" /> directly. That public factory is the
/// shared door both middleware sit on: fixing only the call sites would leave the API that invites the defect
/// shipped, and the next caller reopens it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AuthorizationDenialDisclosureShould : UnitTestBase
{
	/// <summary>
	/// Deliberately distinctive: a generic name like "AdminPolicy" could appear in a body by coincidence, which
	/// would make an absence assertion pass for the wrong reason.
	/// </summary>
	private const string SecretPolicyName = "Sup3rSecretPolicyNameXyzzy";

	private const string SecretRoleName = "Sup3rSecretRoleNameXyzzy";

	private readonly IHttpContextAccessor _httpContextAccessor = A.Fake<IHttpContextAccessor>();
	/// <summary>
	/// The REAL authorization stack. The secret policy is registered as one nothing in these arms can
	/// satisfy, so the denial is produced by the host's own evaluation rather than by a double told to fail —
	/// which matters here, because what is under test is what the denial DISCLOSES.
	/// </summary>
	private readonly (IAuthorizationPolicyProvider Provider, IAuthorizationService Service, ServiceProvider Container) _authorization =
		TestAuthorizationHost.Build(static options =>
			options.AddPolicy(SecretPolicyName, static policy => policy.RequireClaim("a-claim-nobody-here-carries")));
	private readonly CapturingLogger<AspNetCoreAuthorizationMiddleware> _logger = new();

	[Fact]
	public async Task NotDisclosePolicyNameInTheDenialBody()
	{
		// Arrange
		ArrangeAuthenticatedUserWithoutRole();
		// Act
		var result = await InvokeAsync(new PolicyProtectedMessage());

		// Assert — SAFETY: the policy name reaches neither the detail nor any other serialized field.
		result.Succeeded.ShouldBeFalse();
		var body = Serialize(result);
		body.ShouldNotContain(SecretPolicyName);
		result.ProblemDetails!.Detail.ShouldBe("You do not have permission to access this resource");
	}

	[Fact]
	public async Task StillRefuseAndLogThePolicyNameServerSide()
	{
		// Arrange
		ArrangeAuthenticatedUserWithoutRole();
		// Act
		var result = await InvokeAsync(new PolicyProtectedMessage());

		// Assert — LIVENESS: still denied, and the reason a developer needs is in the log.
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails!.Status.ShouldBe(403);
		_logger.HasLogged(LogLevel.Warning, SecretPolicyName)
			.ShouldBeTrue("the denial reason must reach the log, or a developer cannot diagnose a 403");
	}

	[Fact]
	public async Task NotDiscloseRoleNameInTheDenialBody()
	{
		// Arrange
		ArrangeAuthenticatedUserWithoutRole();

		// Act
		var result = await InvokeAsync(new RoleProtectedMessage());

		// Assert — SAFETY
		result.Succeeded.ShouldBeFalse();
		Serialize(result).ShouldNotContain(SecretRoleName);
	}

	[Fact]
	public async Task StillRefuseAndLogTheRoleNameServerSide()
	{
		// Arrange
		ArrangeAuthenticatedUserWithoutRole();

		// Act
		var result = await InvokeAsync(new RoleProtectedMessage());

		// Assert — LIVENESS
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails!.Status.ShouldBe(403);
		_logger.HasLogged(LogLevel.Warning, SecretRoleName)
			.ShouldBeTrue("the denial reason must reach the log, or a developer cannot diagnose a 403");
	}

	/// <summary>
	/// The third door. <see cref="DispatchProblemDetails.ForForbidden" /> is public shipped API whose reason
	/// parameter used to land verbatim in a 403 body; this arm is what stops it reopening.
	/// </summary>
	[Fact]
	public void NotSerializeTheReasonPassedToForForbidden()
	{
		// Act
		var problem = DispatchProblemDetails.ForForbidden(SecretPolicyName);

		// Assert — SAFETY on the wire, LIVENESS server-side.
		JsonSerializer.Serialize(problem).ShouldNotContain(SecretPolicyName);
		problem.Detail.ShouldBe("You do not have permission to access this resource");
		problem.Status.ShouldBe(403);
		problem.DiagnosticReason.ShouldBe(SecretPolicyName);
	}

	/// <inheritdoc cref="NotSerializeTheReasonPassedToForForbidden" />
	[Fact]
	public void NotSerializeTheReasonPassedToForUnauthorized()
	{
		// Act
		var problem = DispatchProblemDetails.ForUnauthorized(SecretPolicyName);

		// Assert
		JsonSerializer.Serialize(problem).ShouldNotContain(SecretPolicyName);
		problem.Detail.ShouldBe("Authentication is required to access this resource");
		problem.Status.ShouldBe(401);
		problem.DiagnosticReason.ShouldBe(SecretPolicyName);
	}

	#region Helpers

	private void ArrangeAuthenticatedUserWithoutRole()
	{
		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.Name, "test-user")],
			authenticationType: "Test");

		A.CallTo(() => _httpContextAccessor.HttpContext)
			.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
	}

	private async Task<IMessageResult> InvokeAsync(IDispatchMessage message)
	{
		var middleware = new AspNetCoreAuthorizationMiddleware(
			_httpContextAccessor,
			_authorization.Service,
			_authorization.Provider,
			TestHandlerRegistry.KnowingEveryMessageType,
			Microsoft.Extensions.Options.Options.Create(new AspNetCoreAuthorizationOptions()),
			_logger);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.CorrelationId).Returns("corr-1");

		return await middleware.InvokeAsync(
			message,
			context,
			(_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>()),
			CancellationToken.None);
	}

	/// <summary>
	/// Serializes what the caller actually receives. Asserting on <c>Detail</c> alone would miss a leak that moved
	/// into <c>Title</c>, <c>Instance</c> or an extension member.
	/// </summary>
	private static string Serialize(IMessageResult result) =>
		JsonSerializer.Serialize(result.ProblemDetails);

	[Authorize(Policy = SecretPolicyName)]
	private sealed class PolicyProtectedMessage : IDispatchMessage;

	[Authorize(Roles = SecretRoleName)]
	private sealed class RoleProtectedMessage : IDispatchMessage;

	#endregion
}
