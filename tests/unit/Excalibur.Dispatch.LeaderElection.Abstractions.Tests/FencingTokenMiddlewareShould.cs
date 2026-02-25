// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="FencingTokenMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FencingTokenMiddlewareShould : UnitTestBase
{
	private readonly IFencingTokenProvider _provider = A.Fake<IFencingTokenProvider>();
	private readonly FencingTokenMiddleware _middleware;
	private readonly IDispatchMessage _message = A.Fake<IDispatchMessage>();

	public FencingTokenMiddlewareShould()
	{
		_middleware = new FencingTokenMiddleware(_provider, NullLogger<FencingTokenMiddleware>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullProvider_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new FencingTokenMiddleware(null!, NullLogger<FencingTokenMiddleware>.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new FencingTokenMiddleware(_provider, null!));
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void Stage_IsAuthorization()
	{
		// Assert
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.Authorization);
	}

	[Fact]
	public void ApplicableMessageKinds_IncludesActionAndEvent()
	{
		// Assert
		_middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Tests

	[Fact]
	public async Task InvokeAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _middleware.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _middleware.InvokeAsync(_message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_WithNullNextDelegate_ThrowsArgumentNullException()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _middleware.InvokeAsync(_message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_WithoutFencingTokenInContext_CallsNextDelegate()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingTokenKey)).Returns(false);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithoutResourceIdInContext_CallsNextDelegate()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingTokenKey)).Returns(true);
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingResourceIdKey)).Returns(false);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithEmptyResourceId_CallsNextDelegate()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingTokenKey)).Returns(true);
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingResourceIdKey)).Returns(true);
		A.CallTo(() => context.GetItem<long>(FencingTokenMiddleware.FencingTokenKey)).Returns(42L);
		A.CallTo(() => context.GetItem<string>(FencingTokenMiddleware.FencingResourceIdKey)).Returns(string.Empty);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithValidToken_CallsNextDelegate()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingTokenKey)).Returns(true);
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingResourceIdKey)).Returns(true);
		A.CallTo(() => context.GetItem<long>(FencingTokenMiddleware.FencingTokenKey)).Returns(42L);
		A.CallTo(() => context.GetItem<string>(FencingTokenMiddleware.FencingResourceIdKey)).Returns("resource-1");
		A.CallTo(() => context.MessageId).Returns("msg-1");

#pragma warning disable CA2012
		A.CallTo(() => _provider.ValidateTokenAsync("resource-1", 42L, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));
#pragma warning restore CA2012

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithStaleToken_ReturnsFailed()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingTokenKey)).Returns(true);
		A.CallTo(() => context.ContainsItem(FencingTokenMiddleware.FencingResourceIdKey)).Returns(true);
		A.CallTo(() => context.GetItem<long>(FencingTokenMiddleware.FencingTokenKey)).Returns(10L);
		A.CallTo(() => context.GetItem<string>(FencingTokenMiddleware.FencingResourceIdKey)).Returns("resource-1");
		A.CallTo(() => context.MessageId).Returns("msg-1");

#pragma warning disable CA2012
		A.CallTo(() => _provider.ValidateTokenAsync("resource-1", 10L, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));
#pragma warning restore CA2012

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeFalse();
		result.IsSuccess.ShouldBeFalse();
	}

	#endregion

	#region Constants Tests

	[Fact]
	public void FencingTokenKey_HasExpectedValue()
	{
		FencingTokenMiddleware.FencingTokenKey.ShouldBe("FencingToken");
	}

	[Fact]
	public void FencingResourceIdKey_HasExpectedValue()
	{
		FencingTokenMiddleware.FencingResourceIdKey.ShouldBe("FencingResourceId");
	}

	#endregion
}
