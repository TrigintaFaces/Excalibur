// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
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
		var items = new Dictionary<string, object>();
		A.CallTo(() => context.Items).Returns(items);

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
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithoutResourceIdInContext_CallsNextDelegate()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = 42L,
		};
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = 42L,
			[FencingTokenMiddleware.FencingResourceIdKey] = string.Empty,
		};
		A.CallTo(() => context.Items).Returns(items);

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
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = 42L,
			[FencingTokenMiddleware.FencingResourceIdKey] = "resource-1",
		};
		A.CallTo(() => context.Items).Returns(items);
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
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithStaleToken_ReturnsFailed()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = 10L,
			[FencingTokenMiddleware.FencingResourceIdKey] = "resource-1",
		};
		A.CallTo(() => context.Items).Returns(items);
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
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Strictly-Positive Token Tests

	/// <summary>
	/// A fencing token is strictly positive, so a non-positive value is not a token and must be refused
	/// rather than validated. Zero is the load-bearing case: it is what <c>GetItem&lt;long&gt;</c> yields
	/// for an item of the wrong type, and it is the one value a provider whose own high-water mark is
	/// unset-as-zero would ADMIT under a <c>token &gt;= mark</c> comparison.
	/// </summary>
	/// <remarks>
	/// The provider is asserted to have been left unconsulted, not merely that the message was rejected.
	/// A middleware that forwarded a zero and happened to be refused by a correct provider would satisfy
	/// a result-only assertion while still depending on every provider getting it right -- which is the
	/// coupling this guard exists to remove.
	/// </remarks>
	[Theory]
	[InlineData(0L)]
	[InlineData(-1L)]
	public async Task InvokeAsync_WithNonPositiveToken_RejectsWithoutConsultingTheProvider(long token)
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = token,
			[FencingTokenMiddleware.FencingResourceIdKey] = "resource-1",
		};
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.MessageId).Returns("msg-1");

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeFalse($"a token of {token} is not a fencing token and must not reach the handler");
		result.Succeeded.ShouldBeFalse($"a token of {token} must be rejected, not admitted");
#pragma warning disable CA2012
		A.CallTo(() => _provider.ValidateTokenAsync(A<string>._, A<long>._, A<CancellationToken>._))
			.MustNotHaveHappened();
#pragma warning restore CA2012
	}

	/// <summary>
	/// An item present under the token key but holding the wrong type reads as zero, which is
	/// indistinguishable from a genuine zero at the read and must be refused the same way.
	/// </summary>
	[Fact]
	public async Task InvokeAsync_WithWronglyTypedTokenItem_Rejects()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = "not-a-token",
			[FencingTokenMiddleware.FencingResourceIdKey] = "resource-1",
		};
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.MessageId).Returns("msg-1");

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(_message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeFalse("a wrongly-typed token item reads as zero, which is not a token");
		result.Succeeded.ShouldBeFalse("a wrongly-typed token item must be rejected, not admitted");
	}

	#endregion

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task PreserveTheOriginalAuthorityFaultWithoutInvokingNext(bool timeout)
	{
		Exception failure = timeout ? new TimeoutException("authority timeout") : new IOException("authority unavailable");
		using var cancellation = new CancellationTokenSource();
		var reader = new FaultingReader(failure);
		var middleware = new FencingTokenMiddleware(reader, NullLogger<FencingTokenMiddleware>.Instance);
		var nextCalls = 0;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalls++;
			return ValueTask.FromResult(MessageResult.Success());
		};

		Func<Task> invoke = async () =>
			await middleware.InvokeAsync(_message, GuardedContext(), next, cancellation.Token);
		Exception observed = timeout
			? await Should.ThrowAsync<TimeoutException>(invoke)
			: await Should.ThrowAsync<IOException>(invoke);

		observed.ShouldBeSameAs(failure);
		nextCalls.ShouldBe(0);
		reader.Calls.ShouldBe(1);
		reader.ResourceId.ShouldBe("authority-resource");
		reader.Token.ShouldBe(17L);
		reader.Cancellation.ShouldBe(cancellation.Token);
	}

	[Fact]
	public async Task PreserveTheOriginalCancellationAndTokenWithoutInvokingNext()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var failure = new OperationCanceledException("authority canceled", null, cancellation.Token);
		var reader = new FaultingReader(failure);
		var middleware = new FencingTokenMiddleware(reader, NullLogger<FencingTokenMiddleware>.Instance);
		var nextCalls = 0;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalls++;
			return ValueTask.FromResult(MessageResult.Success());
		};

		OperationCanceledException? observed = null;
		try
		{
			await middleware.InvokeAsync(_message, GuardedContext(), next, cancellation.Token);
		}
		catch (OperationCanceledException exception)
		{
			observed = exception;
		}

		observed.ShouldNotBeNull();

		observed.ShouldBeSameAs(failure);
		observed.CancellationToken.ShouldBe(cancellation.Token);
		nextCalls.ShouldBe(0);
		reader.Calls.ShouldBe(1);
		reader.ResourceId.ShouldBe("authority-resource");
		reader.Token.ShouldBe(17L);
		reader.Cancellation.ShouldBe(cancellation.Token);
	}

	private static IMessageContext GuardedContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			[FencingTokenMiddleware.FencingTokenKey] = 17L,
			[FencingTokenMiddleware.FencingResourceIdKey] = "authority-resource",
		});
		return context;
	}

	private sealed class FaultingReader(Exception failure) : IFencingTokenProvider
	{
		public int Calls { get; private set; }
		public string? ResourceId { get; private set; }
		public long Token { get; private set; }
		public CancellationToken Cancellation { get; private set; }

		public ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Middleware must use validation, not reread the mark.");

		public ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
		{
			Calls++;
			ResourceId = resourceId;
			Token = token;
			Cancellation = cancellationToken;
			return ValueTask.FromException<bool>(failure);
		}
	}

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
