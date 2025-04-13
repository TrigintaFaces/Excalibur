using DOPA;

using Excalibur.A3.Authorization;
using Excalibur.Core;
using Excalibur.Tests.Wrappers;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public sealed class AuthorizationPolicyShould : IDisposable
{
	private readonly IOpaPolicy _policy;
	private readonly ITenantId _tenantId;
	private readonly string _userId;
	private readonly AuthorizationPolicyWrapper _authPolicy;

	public AuthorizationPolicyShould()
	{
		_policy = A.Fake<IOpaPolicy>();
		_tenantId = A.Fake<ITenantId>();
		_userId = "test-user-123";

		_ = A.CallTo(() => _tenantId.Value).Returns("test-tenant-456");

		_authPolicy = new AuthorizationPolicyWrapper(_policy, _tenantId, _userId);
	}

	public void Dispose()
	{
		_policy.Dispose();
		_authPolicy.Dispose();
	}

	[Fact]
	public void InitializeWithCorrectValues()
	{
		// Assert
		_authPolicy.UserId.ShouldBe(_userId);
		_authPolicy.TenantId.ShouldBe("test-tenant-456");
	}

	[Fact]
	public void ReturnTrueWhenIsAuthorizedPolicyEvaluatesTrue()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = true, HasActivityGrant = false, HasResourceGrant = false });

		// Act
		var result = _authPolicy.IsAuthorized("TestActivity", "resource123");

		// Assert
		result.ShouldBeTrue();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnFalseWhenIsAuthorizedPolicyEvaluatesFalse()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = false, HasResourceGrant = false });

		// Act
		var result = _authPolicy.IsAuthorized("TestActivity", "resource123");

		// Assert
		result.ShouldBeFalse();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnTrueWhenHasActivityGrantPolicyEvaluatesTrue()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = true, HasResourceGrant = false });

		// Act
		var result = _authPolicy.HasGrant("TestActivity");

		// Assert
		result.ShouldBeTrue();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnFalseWhenHasActivityGrantPolicyEvaluatesFalse()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = false, HasResourceGrant = false });

		// Act
		var result = _authPolicy.HasGrant("TestActivity");

		// Assert
		result.ShouldBeFalse();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnTrueWhenHasActivityGrantForGenericActivityEvaluatesTrue()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = true, HasResourceGrant = false });

		// Act
		var result = _authPolicy.HasGrant<TestActivity>();

		// Assert
		result.ShouldBeTrue();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnTrueWhenHasResourceGrantPolicyEvaluatesTrue()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = false, HasResourceGrant = true });

		// Act
		var result = _authPolicy.HasGrant("TestResource", "resource123");

		// Assert
		result.ShouldBeTrue();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnFalseWhenHasResourceGrantPolicyEvaluatesFalse()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = false, HasResourceGrant = false });

		// Act
		var result = _authPolicy.HasGrant("TestResource", "resource123");

		// Assert
		result.ShouldBeFalse();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnTrueWhenHasGrantForGenericResourceTypeEvaluatesTrue()
	{
		// Arrange
		SetupPolicyToReturn(new { IsAuthorized = false, HasActivityGrant = false, HasResourceGrant = true });

		// Act
		var result = _authPolicy.HasGrant<TestResource>("resource123");

		// Assert
		result.ShouldBeTrue();
		VerifyPolicyWasEvaluated();
	}

	[Fact]
	public void ReturnFalseWhenPolicyReturnsNull()
	{
		// Arrange - Policy returns null
		_ = A.CallTo(() => _policy.Evaluate<object>(A<object>._)).Returns(null);

		// Act
		var authorizedResult = _authPolicy.IsAuthorized("TestActivity");
		var activityGrantResult = _authPolicy.HasGrant("TestActivity");
		var resourceGrantResult = _authPolicy.HasGrant("TestResource", "resource123");

		// Assert
		authorizedResult.ShouldBeFalse();
		activityGrantResult.ShouldBeFalse();
		resourceGrantResult.ShouldBeFalse();
	}

	private void SetupPolicyToReturn(dynamic values)
	{
		var result = new AuthorizationPolicy.PolicyResult
		{
			IsAuthorized = values.IsAuthorized,
			HasActivityGrant = values.HasActivityGrant,
			HasResourceGrant = values.HasResourceGrant
		};

		_ = A.CallTo(() => _policy.Evaluate<AuthorizationPolicy.PolicyResult>(A<object>._))
			.Returns(result);
	}

	private void VerifyPolicyWasEvaluated()
	{
		_ = A.CallTo(() => _policy.Evaluate<AuthorizationPolicy.PolicyResult>(
				A<object>.That.Matches(input =>
					input.GetType().GetProperty("TenantId") != null &&
					input.GetType().GetProperty("now") != null)))
			.MustHaveHappenedOnceExactly();
	}

	// Test classes
	private sealed class TestActivity
	{
	}

	private sealed class TestResource
	{
	}
}
