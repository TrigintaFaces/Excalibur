#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy needs stored ValueTask

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Depth unit tests for <see cref="AuthorizationMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationMiddlewareDepthShould
{
	private readonly IAccessToken _accessToken;
	private readonly IDispatchAuthorizationService _authorization;
	private readonly AttributeAuthorizationCache _attributeCache;
	private readonly AuthorizationMiddleware _sut;

	public AuthorizationMiddlewareDepthShould()
	{
		_accessToken = A.Fake<IAccessToken>();
		_authorization = A.Fake<IDispatchAuthorizationService>();
		_attributeCache = new AttributeAuthorizationCache();

		A.CallTo(() => _accessToken.IsAuthenticated()).Returns(true);

		_sut = new AuthorizationMiddleware(_accessToken, _authorization, _attributeCache);
	}

	[Fact]
	public void Implement_IDispatchMiddleware()
	{
		_sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void HaveAuthorizationStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SkipAuthorizationForNonAuthorizableMessage()
	{
		// Arrange — message does NOT implement IRequireAuthorization
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _authorization.AuthorizeAsync(
			A<System.Security.Claims.ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement[]>.Ignored))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnFailedResult_WhenAuthorizationFails()
	{
		// Arrange
		var message = new TestAuthorizableMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		A.CallTo(() => _authorization.AuthorizeAsync(
			A<System.Security.Claims.ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement[]>.Ignored))
			.Returns(AuthorizationResult.Failed("Not authorized"));

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ProceedToNext_WhenAuthorizationSucceeds()
	{
		// Arrange
		var message = new TestAuthorizableMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		A.CallTo(() => _authorization.AuthorizeAsync(
			A<System.Security.Claims.ClaimsPrincipal>.Ignored,
			A<object?>.Ignored,
			A<Microsoft.AspNetCore.Authorization.IAuthorizationRequirement[]>.Ignored))
			.Returns(AuthorizationResult.Success());

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	private sealed class TestAuthorizableMessage : IDispatchMessage, IRequireAuthorization
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public MessageKinds Kind => MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().Name;
		public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();
		public string ActivityName => "TestActivity";
		public string AccessToken { get; set; } = "test-token";
	}
}
