// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.AspNetCore.Authorization;

using A3AttributeAuthorizationCache = Excalibur.A3.Authorization.AttributeAuthorizationCache;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="A3AuthorizationMiddleware"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationMiddlewareShould : IDisposable
{
	private readonly IAccessToken _accessToken;
	private readonly IDispatchAuthorizationService _authorizationService;
	private readonly A3AttributeAuthorizationCache _attributeCache;
	private readonly A3AuthorizationMiddleware _sut;
	private readonly ServiceProvider _serviceProvider;
	private readonly IServiceScope _requestScope;

	public AuthorizationMiddlewareShould()
	{
		_accessToken = A.Fake<IAccessToken>();
		_authorizationService = A.Fake<IDispatchAuthorizationService>();
		_attributeCache = new A3AttributeAuthorizationCache();
		_sut = new A3AuthorizationMiddleware(_authorizationService, _attributeCache, new ConditionExpressionEvaluator());

		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		_ = services.AddScoped(_ => _accessToken);
		_serviceProvider = services.BuildServiceProvider();
		_requestScope = _serviceProvider.CreateScope();
	}
	/// <summary>
	/// Builds a message context carrying a real request scope, which is where the middleware now reads
	/// the caller's access token from. A fake provider is not enough: the middleware rejects anything
	/// that is not an <see cref="IServiceScope"/> so a root provider cannot supply an identity.
	/// </summary>
	private IMessageContext FakeContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.RequestServices).Returns(_requestScope.ServiceProvider);
		return context;
	}

	public void Dispose()
	{
		_requestScope.Dispose();
		_serviceProvider.Dispose();
	}


	[Fact]
	public void ImplementIDispatchMiddleware()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert - T.7: middleware classes moved to internal visibility
		typeof(A3AuthorizationMiddleware).IsPublic.ShouldBeFalse();
		typeof(A3AuthorizationMiddleware).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveAuthorizationStage()
	{
		// Assert
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Arrange
		var context = FakeContext();
		var nextDelegate = A.Fake<DispatchRequestDelegate>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(null!, context, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var nextDelegate = A.Fake<DispatchRequestDelegate>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(message, null!, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenNextDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = FakeContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SkipAuthorizationWhenMessageDoesNotRequireIt()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = FakeContext();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<string>.Ignored,
			A<IAuthorizationRequirement[]>.Ignored))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CallAuthorizationServiceForMessageRequiringAuthorization()
	{
		// Arrange
		var message = new TestAuthorizableMessage();

		var context = FakeContext();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

		A.CallTo(() => _accessToken.Claims).Returns([]);
		A.CallTo(() => _accessToken.IsAuthenticated()).Returns(true);

		var authResult = Excalibur.Dispatch.AuthorizationResult.Success();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<string>.Ignored,
			A<IAuthorizationRequirement[]>.Ignored))
			.Returns(authResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<string>.That.IsEqualTo("TestActivity"),
			A<IAuthorizationRequirement[]>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnFailedResultWhenAuthorizationFails()
	{
		// Arrange
		var message = new TestAuthorizableMessage();

		var context = FakeContext();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		A.CallTo(() => _accessToken.Claims).Returns([]);
		A.CallTo(() => _accessToken.IsAuthenticated()).Returns(false);

		var authResult = Excalibur.Dispatch.AuthorizationResult.Failed("Unauthorized");
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<string>.Ignored,
			A<IAuthorizationRequirement[]>.Ignored))
			.Returns(authResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task ProceedToNextDelegateWhenAuthorized()
	{
		// Arrange
		var message = new TestAuthorizableMessage();

		var context = FakeContext();
		var nextDelegateCalled = false;
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate nextDelegate = (_, _, _) =>
		{
			nextDelegateCalled = true;
			return ValueTask.FromResult(expectedResult);
		};

		A.CallTo(() => _accessToken.Claims).Returns([]);
		A.CallTo(() => _accessToken.IsAuthenticated()).Returns(true);

		var authResult = Excalibur.Dispatch.AuthorizationResult.Success();
		A.CallTo(() => _authorizationService.AuthorizeAsync(
			A<ClaimsPrincipal>.Ignored,
			A<string>.Ignored,
			A<IAuthorizationRequirement[]>.Ignored))
			.Returns(authResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		nextDelegateCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	/// <summary>
	/// Test message that implements IRequireAuthorization.
	/// </summary>
	private sealed class TestAuthorizableMessage : IDispatchMessage, IRequireAuthorization
	{
		public Guid MessageId { get; init; } = Guid.NewGuid();
		public string ActivityName => "TestActivity";
	}
}
