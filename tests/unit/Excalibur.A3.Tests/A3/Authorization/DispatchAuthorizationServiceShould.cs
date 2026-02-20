using System.Security.Claims;

using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;

using AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult;
using MsAuthorizationResult = Microsoft.AspNetCore.Authorization.AuthorizationResult;

namespace Excalibur.Tests.A3.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class DispatchAuthorizationServiceShould
{
	private readonly IAuthorizationService _innerService;
	private readonly DispatchAuthorizationService _sut;

	public DispatchAuthorizationServiceShould()
	{
		_innerService = A.Fake<IAuthorizationService>();
		_sut = new DispatchAuthorizationService(_innerService);
	}

	[Fact]
	public void Implement_IDispatchAuthorizationService()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDispatchAuthorizationService>();
	}

	[Fact]
	public async Task Return_success_when_requirements_authorization_succeeds()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, requirement);

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_failure_when_requirements_authorization_fails()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Failed());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, requirement);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public async Task Return_success_when_policy_authorization_succeeds()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, policyName);

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_failure_when_policy_authorization_fails()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Failed());

		// Act
		var result = await _sut.AuthorizeAsync(user, null, policyName);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public async Task Pass_resource_to_inner_service_for_requirements()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var resource = "test-resource";
		var requirement = A.Fake<IAuthorizationRequirement>();

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		await _sut.AuthorizeAsync(user, resource, requirement);

		// Assert
		A.CallTo(() => _innerService.AuthorizeAsync(
			user,
			resource,
			A<IEnumerable<IAuthorizationRequirement>>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Pass_resource_to_inner_service_for_policy()
	{
		// Arrange
		var user = new ClaimsPrincipal(new ClaimsIdentity());
		var resource = "test-resource";
		var policyName = "TestPolicy";

		A.CallTo(() => _innerService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<string>.Ignored))
			.Returns(MsAuthorizationResult.Success());

		// Act
		await _sut.AuthorizeAsync(user, resource, policyName);

		// Assert
		A.CallTo(() => _innerService.AuthorizeAsync(
			user,
			resource,
			policyName))
			.MustHaveHappenedOnceExactly();
	}
}
