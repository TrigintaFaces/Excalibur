using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Requests;
using Excalibur.A3.Exceptions;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public sealed class AuthorizationBehaviorShould
{
	private readonly IAccessToken _accessToken = A.Fake<IAccessToken>();

	[Fact]
	public async Task BypassAuthorizationForNonAuthorizableRequests()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestNonAuthorizableRequest, string>(_accessToken);
		var request = new TestNonAuthorizableRequest();

		var delegateCalled = false;

		async Task<string> NextDelegate()
		{
			delegateCalled = true;
			return await Task.FromResult("Success").ConfigureAwait(true);
		}

		// Act
		var result = await behavior.Handle(request, NextDelegate, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");
		delegateCalled.ShouldBeTrue();

		// Verify no authorization check was performed
		A.CallTo(() => _accessToken.IsAuthorized(A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowWhenUserIsNotAuthenticated()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);
		var request = new TestAuthorizableRequest("resource123");

		// Setup token to return anonymous state
		_ = A.CallTo(() => _accessToken.IsAnonymous()).Returns(true);

		// Act & Assert
		var exception = await Should.ThrowAsync<NotAuthorizedException>(async () =>
		{
			_ = await behavior.Handle(request, () => Task.FromResult("Success"), CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

		exception.Message.ShouldContain("Anonymous access is not allowed");
		exception.StatusCode.ShouldBe(401);
	}

	[Fact]
	public async Task ThrowWhenUserIsNotAuthorized()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);
		var request = new TestAuthorizableRequest("resource123");

		// Setup token to return authenticated but not authorized
		_ = A.CallTo(() => _accessToken.IsAnonymous()).Returns(false);
		_ = A.CallTo(() => _accessToken.IsAuthorized(nameof(TestAuthorizableRequest), "resource123")).Returns(false);

		// Act & Assert
		var exception = await Should.ThrowAsync<NotAuthorizedException>(async () =>
		{
			_ = await behavior.Handle(request, () => Task.FromResult("Success"), CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

		exception.Message.ShouldContain("is not authorized");
		exception.StatusCode.ShouldBe(403);
	}

	[Fact]
	public async Task ThrowWhenResourceIdIsNotProvided()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);
		var request = new TestAuthorizableRequest(string.Empty);

		// Setup token to return authenticated
		_ = A.CallTo(() => _accessToken.IsAnonymous()).Returns(false);

		// Act & Assert
		var exception = await Should.ThrowAsync<NotAuthorizedException>(async () =>
		{
			_ = await behavior.Handle(request, () => Task.FromResult("Success"), CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

		exception.Message.ShouldContain("is not authorized");
	}

	[Fact]
	public async Task ProceedWhenUserIsAuthorized()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);
		var request = new TestAuthorizableRequest("resource123");

		// Setup token to return authenticated and authorized
		_ = A.CallTo(() => _accessToken.IsAnonymous()).Returns(false);
		_ = A.CallTo(() => _accessToken.IsAuthorized(nameof(TestAuthorizableRequest), "resource123")).Returns(true);

		// Act
		var result = await behavior.Handle(request, () => Task.FromResult("Success"), CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe("Success");
		request.AccessToken.ShouldBeSameAs(_accessToken);
	}

	[Fact]
	public async Task ThrowWhenRequestIsNull()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			// ReSharper disable once ExpressionIsAlwaysNull
			_ = await behavior.Handle(null!, () => Task.FromResult("Success"), CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowWhenNextDelegateIsNull()
	{
		// Arrange
		var behavior = new AuthorizationBehavior<TestAuthorizableRequest, string>(_accessToken);
		var request = new TestAuthorizableRequest("resource123");

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			// ReSharper disable once ExpressionIsAlwaysNull
			_ = await behavior.Handle(request, null!, CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);
	}

	// Test classes
	private sealed class TestNonAuthorizableRequest
	{
		// Minimal request implementation that is not authorizable
	}

	private sealed class TestAuthorizableRequest(string resourceId) : IAmAuthorizable, IAmAuthorizableForResource
	{
		// Constructor to initialize ResourceId

		public string ResourceId { get; } = resourceId;
		public string[] ResourceTypes => ["TestResource"];
		public IAccessToken? AccessToken { get; set; }
	}
}
